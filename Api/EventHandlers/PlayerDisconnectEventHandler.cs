using Api.EventHandlers.Dtos;
using Api.WebSockets;
using Fleck;
using Microsoft.EntityFrameworkCore;
using EFScaffold;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class PlayerDisconnectEventHandler : BaseEventHandler<PlayerDisconnectDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;
        private readonly ILogger<PlayerDisconnectEventHandler> _logger;

        public PlayerDisconnectEventHandler(
            IConnectionManager connectionManager,
            KahootContext context,
            ILogger<PlayerDisconnectEventHandler> logger)
        {
            _connectionManager = connectionManager;
            _context = context;
            _logger = logger;
        }

        public override async Task Handle(PlayerDisconnectDto dto, IWebSocketConnection socket)
        {
            try
            {
                // Get client ID directly from socket connection
                string clientId = await _connectionManager.GetClientIdFromSocketId(socket.ConnectionInfo.Id.ToString());
                _logger.LogInformation($"Processing explicit disconnect for clientId: {clientId}");
        
                // Use the connection manager's OnClose method for consistency
                // This ensures the same cleanup logic runs for both explicit and implicit disconnects
                await _connectionManager.OnClose(socket, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling explicit player disconnect");
            }
        }

        private async Task CleanupPlayerRecord(string clientId)
        {
            _logger.LogInformation($"Looking up player with client ID: {clientId}");
            
            // Find player by client ID
            var player = await _context.Players.FindAsync(clientId);
            if (player != null)
            {
                string gameId = player.GameId;
                string nickname = player.Nickname;
                
                _logger.LogInformation($"Found player {nickname} (ID: {clientId}) in game {gameId}, removing from database");
                
                _context.Players.Remove(player);
                await _context.SaveChangesAsync();
                
                // Update other players about this player leaving
                if (!string.IsNullOrEmpty(gameId))
                {
                    var remainingPlayers = await _context.Players
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
                    
                    await _connectionManager.BroadcastToTopic($"game:{gameId}", updateDto);
                    _logger.LogInformation($"Broadcast player removal to game:{gameId}");
                }
            }
            else
            {
                _logger.LogWarning($"No player found with client ID: {clientId}");
            }
        }
    }
}