using Api.EventHandlers.Dtos;
using Api.WebSockets;
using EFScaffold;
using Fleck;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class JoinGameEventHandler : BaseEventHandler<JoinGameRequestDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;

        public JoinGameEventHandler(IConnectionManager connectionManager, KahootContext context)
        {
            _connectionManager = connectionManager;
            _context = context;
        }

        public override async Task Handle(JoinGameRequestDto dto, IWebSocketConnection socket)
        {
            Console.WriteLine($"JoinGameRequestDto received for game {dto.GameId} with nickname {dto.Nickname}");
            
            var clientId = await _connectionManager.GetClientIdFromSocketId(socket.ConnectionInfo.Id.ToString());
            
            try
            {
                // Check if the game exists
                var game = await _context.Games.FindAsync(dto.GameId);
                if (game == null)
                {
                    var errorResponse = new JoinGameResponseDto
                    {
                        eventType = "JoinGameResponseDto",
                        requestId = dto.requestId,
                        Success = false,
                        Message = "Game not found."
                    };
                    socket.SendDto(errorResponse);
                    return;
                }

                // Check if nickname is already taken in this game
                var nicknameExists = await _context.Players
                    .AnyAsync(p => p.GameId == dto.GameId && p.Nickname == dto.Nickname);

                if (nicknameExists)
                {
                    var errorResponse = new JoinGameResponseDto
                    {
                        eventType = "JoinGameResponseDto",
                        requestId = dto.requestId,
                        Success = false,
                        Message = "Nickname already taken in this game. Please choose another."
                    };
                    socket.SendDto(errorResponse);
                    return;
                }

                // Create new player record
                var player = new EFScaffold.EntityFramework.Player
                {
                    Id = clientId,
                    GameId = dto.GameId,
                    Nickname = dto.Nickname
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                // Add client to game topic for broadcasts
                string gameTopic = $"game:{dto.GameId}";
                await _connectionManager.AddToTopic($"game:{dto.GameId}", clientId);

                // Send success response to client
                var successResponse = new JoinGameResponseDto
                {
                    eventType = "JoinGameResponseDto",
                    requestId = dto.requestId,
                    Success = true,
                    Message = "Successfully joined the game."
                };
                socket.SendDto(successResponse);

                // Send updated players list to all clients in this game
                await SendPlayersUpdate(dto.GameId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error joining game: {ex.Message}");
                var errorResponse = new JoinGameResponseDto
                {
                    eventType = "JoinGameResponseDto",
                    requestId = dto.requestId,
                    Success = false,
                    Message = $"Error joining game: {ex.Message}"
                };
                socket.SendDto(errorResponse);
            }
        }

        private async Task SendPlayersUpdate(string gameId)
        {
            var players = await _context.Players
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
            await _connectionManager.BroadcastToTopic(gameTopic, updateDto);
        }
    }
}