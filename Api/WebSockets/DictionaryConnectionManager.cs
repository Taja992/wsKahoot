using System.Collections.Concurrent;
using System.Text.Json;
using Api.EventHandlers.Dtos;
using EFScaffold;
using Fleck;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebSocketBoilerplate;

namespace Api.WebSockets;

public class DictionaryConnectionManager : IConnectionManager
{
    private readonly ILogger<DictionaryConnectionManager> logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DictionaryConnectionManager(ILogger<DictionaryConnectionManager> logger, IServiceScopeFactory serviceScopeFactory)
    {
        this.logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public ConcurrentDictionary<string, HashSet<string>> TopicMembers { get; set; } = new();
    public ConcurrentDictionary<string, HashSet<string>> MemberTopics { get; set; } = new();
    public ConcurrentDictionary<string /* Client ID */, IWebSocketConnection> ConnectionIdToSocket { get; } = new();
    public ConcurrentDictionary<string /* Socket ID */, string /* Client ID */> SocketToConnectionId { get; } = new();

    public Task<ConcurrentDictionary<string, HashSet<string>>> GetAllTopicsWithMembers()
    {
        return Task.FromResult(TopicMembers);
    }

    public Task<ConcurrentDictionary<string, HashSet<string>>> GetAllMembersWithTopics()
    {
        return Task.FromResult(MemberTopics);
    }

    public Task<Dictionary<string, string>> GetAllConnectionIdsWithSocketId()
    {
        return Task.FromResult(ConnectionIdToSocket.ToDictionary(k => k.Key, v => v.Value.ConnectionInfo.Id.ToString())
        );
    }

    public Task<Dictionary<string, string>> GetAllSocketIdsWithConnectionId()
    {
        return Task.FromResult(SocketToConnectionId.ToDictionary(k => k.Key, v => v.Value));
    }

    public async Task AddToTopic(string topic, string memberId, TimeSpan? expiry = null)
    {
        TopicMembers.AddOrUpdate(
            topic,
            _ => new HashSet<string> { memberId },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(memberId);
                    return existing;
                }
            });

        MemberTopics.AddOrUpdate(
            memberId,
            _ => new HashSet<string> { topic },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(topic);
                    return existing;
                }
            });

        await LogCurrentState();
    }

    public Task RemoveFromTopic(string topic, string memberId)
    {
        if (TopicMembers.TryGetValue(topic, out var members))
            lock (members)
            {
                members.Remove(memberId);
            }

        if (MemberTopics.TryGetValue(memberId, out var topics))
            lock (topics)
            {
                topics.Remove(topic);
            }

        return Task.CompletedTask;
    }

    public Task<List<string>> GetMembersFromTopicId(string topic)
    {
        return Task.FromResult(
            TopicMembers.TryGetValue(topic, out var members)
                ? members.ToList()
                : new List<string>());
    }

    public Task<List<string>> GetTopicsFromMemberId(string memberId)
    {
        return Task.FromResult(
            MemberTopics.TryGetValue(memberId, out var topics)
                ? topics.ToList()
                : new List<string>());
    }

    public Task<string> GetClientIdFromSocketId(string socketId)
    {
        var success = SocketToConnectionId.TryGetValue(socketId, out var connectionId);
        if (success)
            return Task.FromResult(connectionId!);
        throw new Exception("Could not find client ID for socket ID " + socketId);
    }

    public async Task OnOpen(IWebSocketConnection socket, string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString();
        }
        
        logger.LogDebug($"OnOpen called with clientId: {clientId} and socketId: {socket.ConnectionInfo.Id}");

        if (ConnectionIdToSocket.TryRemove(clientId, out var oldSocket))
        {
            var oldSocketId = oldSocket.ConnectionInfo.Id.ToString();
            SocketToConnectionId.TryRemove(oldSocketId, out _);
            logger.LogInformation($"Removed old connection {oldSocketId} for client {clientId}");
        }

        ConnectionIdToSocket[clientId] = socket;
        SocketToConnectionId[socket.ConnectionInfo.Id.ToString()] = clientId;

        logger.LogInformation($"Added new connection {socket.ConnectionInfo.Id} for client {clientId}");
        await LogCurrentState();
    }

public async Task OnClose(IWebSocketConnection socket, string clientId)
{
    var socketId = socket.ConnectionInfo.Id.ToString();
    logger.LogInformation($"OnClose triggered for connection: socket={socketId}, client={clientId}");

    if (ConnectionIdToSocket.TryGetValue(clientId, out var currentSocket) &&
        currentSocket.ConnectionInfo.Id.ToString() == socketId)
    {
        ConnectionIdToSocket.TryRemove(clientId, out _);
        logger.LogInformation($"Removed connection for client {clientId}");
    }

    SocketToConnectionId.TryRemove(socketId, out _);

    // Clean up database records
    try
    {
        logger.LogInformation($"Cleaning up player record for clientId: {clientId}");
        
        // Create a new scope for database operations
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KahootContext>();
        
        // Find the player by client ID
        var player = await dbContext.Players.FindAsync(clientId);
        if (player != null)
        {
            string gameId = player.GameId;
            string nickname = player.Nickname;
            
            logger.LogInformation($"Found player {nickname} (ID: {clientId}) in game {gameId}, removing from database");
            
            // Remove the player
            dbContext.Players.Remove(player);
            await dbContext.SaveChangesAsync();
            
            // Notify other players if in a game
            if (!string.IsNullOrEmpty(gameId))
            {
                var remainingPlayers = await dbContext.Players
                    .Where(p => p.GameId == gameId)
                    .Select(p => new GamePlayerDto
                    {
                        Id = p.Id,
                        Nickname = p.Nickname
                    })
                    .ToListAsync();
                
                var updateDto = new GamePlayersUpdateDto
                {
                    eventType = "GamePlayersUpdateDto",
                    GameId = gameId,
                    Players = remainingPlayers
                };
                
                await BroadcastToTopic($"game:{gameId}", updateDto);
            }
        }
        else
        {
            logger.LogInformation($"No player found with ID: {clientId} - nothing to clean up");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Error cleaning up player record for {clientId}");
    }

    // Clean up topic memberships
    if (MemberTopics.TryGetValue(clientId, out var topics))
    {
        foreach (var topic in topics)
        {
            await RemoveFromTopic(topic, clientId);
            await BroadcastToTopic(topic, new MemberHasLeftDto { 
                eventType = "MemberHasLeftDto", 
                MemberId = clientId 
            });
        }
    }

    MemberTopics.TryRemove(clientId, out _);
}

    // Add new method to disconnect all players in a game
    public async Task DisconnectAllPlayersInGame(string gameId)
    {
        logger.LogInformation($"Disconnecting all players in game: {gameId}");
        
        string gameTopic = $"game:{gameId}";
        
        // Get all member IDs in the game topic
        if (!TopicMembers.TryGetValue(gameTopic, out var memberIds))
        {
            logger.LogWarning($"No members found in topic: {gameTopic}");
            return;
        }
        
        // Create a copy of the member IDs to avoid modification during iteration
        var memberIdsCopy = memberIds.ToList();
        
        foreach (var memberId in memberIdsCopy)
        {
            logger.LogInformation($"Disconnecting player: {memberId}");
            
            // Remove player from all topics
            if (MemberTopics.TryGetValue(memberId, out var topics))
            {
                foreach (var topic in topics)
                {
                    await RemoveFromTopic(topic, memberId);
                }
                MemberTopics.TryRemove(memberId, out _);
            }
            
            // Remove socket connection
            if (ConnectionIdToSocket.TryGetValue(memberId, out var socket))
            {
                var socketId = socket.ConnectionInfo.Id.ToString();
                SocketToConnectionId.TryRemove(socketId, out _);
                ConnectionIdToSocket.TryRemove(memberId, out _);
                
                // Force close the WebSocket connection
                try {
                    socket.Close();
                }
                catch (Exception ex) {
                    logger.LogError(ex, $"Error closing socket for {memberId}");
                }
            }
        }
        
        // Clean up the game topic itself
        TopicMembers.TryRemove(gameTopic, out _);
        
        logger.LogInformation($"Disconnected all players in game: {gameId}");
    }

    private async Task BroadcastPlayerLeftUpdate(string gameId, string clientId)
    {
        if (string.IsNullOrEmpty(gameId)) return;
        
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KahootContext>();
        
        try
        {
            var players = await dbContext.Players
                .Where(p => p.GameId == gameId)
                .Select(p => new GamePlayerDto
                {
                    Id = p.Id,
                    Nickname = p.Nickname
                })
                .ToListAsync();

            var updateDto = new GamePlayersUpdateDto
            {
                eventType = "GamePlayersUpdateDto",
                GameId = gameId,
                Players = players
            };

            string gameTopic = $"game:{gameId}";
            await BroadcastToTopic(gameTopic, updateDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error broadcasting player left update for game {gameId}");
        }
    }

    public async Task BroadcastToTopic<T>(string topic, T message) where T : BaseDto
    {
        await LogCurrentState();
        if (!TopicMembers.TryGetValue(topic, out var members))
        {
            logger.LogWarning($"No topic found: {topic}");
            return;
        }

        foreach (var memberId in members.ToList()) 
        {
            await BroadcastToMember(topic, memberId, message);
        }
    }

    private async Task BroadcastToMember<T>(string topic, string memberId, T message) where T : BaseDto
    {
        if (!ConnectionIdToSocket.TryGetValue(memberId, out var socket))
        {
            logger.LogWarning($"No socket found for member: {memberId}");
            await RemoveFromTopic(topic, memberId);
            return;
        }

        if (!socket.IsAvailable)
        {
            logger.LogWarning($"Socket not available for {memberId}");
            await RemoveFromTopic(topic, memberId);
            return;
        }

        socket.SendDto(message);
    }

    public async Task LogCurrentState()
    {
        logger.LogDebug(JsonSerializer.Serialize(new
        {
            ConnectionIdToSocket = await GetAllConnectionIdsWithSocketId(),
            SocketToCnnectionId = await GetAllSocketIdsWithConnectionId(),
            TopicsWithMembers = await GetAllTopicsWithMembers(),
            MembersWithTopics = await GetAllMembersWithTopics()
        }, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}

public class MemberHasLeftDto : BaseDto
{
    public string MemberId { get; set; }
}