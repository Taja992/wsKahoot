using Api.EventHandlers.Dtos;
using Api.WebSockets;
using EFScaffold;
using Fleck;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class GameListEventHandler : BaseEventHandler<GetGamesRequestDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;

        public GameListEventHandler(IConnectionManager connectionManager, KahootContext context)
        {
            _connectionManager = connectionManager;
            _context = context;
        }

        public override async Task Handle(GetGamesRequestDto dto, IWebSocketConnection socket)
        {
            Console.WriteLine("GetGamesRequestDto received");
            
            var games = await _context.Games
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToListAsync();

            Console.WriteLine($"Found {games.Count} games");

            var response = new GetGamesResponseDto
            {
                eventType = "GetGamesResponseDto",
                requestId = dto.requestId,
                Games = games
            };

            Console.WriteLine($"Sending response with {games.Count} games");
            socket.SendDto(response);
        }
    }
}