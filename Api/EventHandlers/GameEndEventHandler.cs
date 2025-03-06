using Api.EventHandlers.Dtos;
using Api.WebSockets;
using EFScaffold;
using Fleck;
using Microsoft.EntityFrameworkCore;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class GameEndEventHandler : BaseEventHandler<GameEndedDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;
        private readonly ILogger<GameEndEventHandler> _logger;

        public GameEndEventHandler(
            IConnectionManager connectionManager,
            KahootContext context,
            ILogger<GameEndEventHandler> logger)
        {
            _connectionManager = connectionManager;
            _context = context;
            _logger = logger;
        }

        public override async Task Handle(GameEndedDto dto, IWebSocketConnection socket)
        {
            if (string.IsNullOrEmpty(dto.GameId))
            {
                _logger.LogError("GameId is null or empty");
                return;
            }

            _logger.LogInformation($"Processing game end for game ID: {dto.GameId}");
            
            try
            {
                // Get all players in this game
                var gamePlayers = await _context.Players
                    .Where(p => p.GameId == dto.GameId)
                    .ToListAsync();
                
                if (gamePlayers.Count == 0)
                {
                    _logger.LogInformation($"No players found for game: {dto.GameId}");
                    return;
                }

                _logger.LogInformation($"Found {gamePlayers.Count} players to clean up for game: {dto.GameId}");

                // Notify all clients in the game topic that the game has ended
                // This will trigger client-side cleanup
                await _connectionManager.BroadcastToTopic($"game:{dto.GameId}", dto);
                
                // Remove all players from database
                _context.Players.RemoveRange(gamePlayers);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Removed all players for game: {dto.GameId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling game end for game: {dto.GameId}");
            }
        }
    }
}
