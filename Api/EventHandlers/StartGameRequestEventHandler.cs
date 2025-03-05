using Api.EventHandlers.Dtos;
using Api.WebSockets;
using EFScaffold;
using Fleck;
using Microsoft.EntityFrameworkCore;
using WebSocketBoilerplate;
using Api.Utilities;

namespace Api.EventHandlers
{
    public class StartGameRequestEventHandler : BaseEventHandler<StartGameRequestDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;
        private readonly IGameTimeProvider _gameTimeProvider;

        public StartGameRequestEventHandler(IConnectionManager connectionManager, KahootContext context, IGameTimeProvider gameTimeProvider)
        {
            _connectionManager = connectionManager;
            _context = context;
            _gameTimeProvider = gameTimeProvider;
        }

        public override async Task Handle(StartGameRequestDto dto, IWebSocketConnection socket)
        {
            var clientId = await _connectionManager.GetClientIdFromSocketId(socket.ConnectionInfo.Id.ToString());

            if (!AdminRequestEventHandler.IsAdmin(clientId))
            {
                var response = new StartGameResponseDto
                {
                    requestId = dto.requestId,
                    Success = false,
                    Message = "Only the admin can start the game."
                };
                socket.SendDto(response);
                return;
            }

            var game = await _context.Games
                .Include(g => g.Questions)
                .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(g => g.Id == dto.GameId);

            if (game == null)
            {
                var response = new StartGameResponseDto
                {
                    requestId = dto.requestId,
                    Success = false,
                    Message = "Game not found."
                };
                socket.SendDto(response);
                return;
            }

            var questions = game.Questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                Options = q.QuestionOptions.Select(o => new QuestionOptionDto
                {
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect
                }).ToList()
            }).ToList();

            var startResponse = new StartGameResponseDto
            {
                requestId = dto.requestId,
                Success = true,
                Message = "Game started."
            };
            socket.SendDto(startResponse);

            await BroadcastQuestionsSequentially(questions);
        }

        private async Task BroadcastQuestionsSequentially(List<QuestionDto> questions)
        {
            foreach (var question in questions)
            {
                question.eventType = "QuestionDto";
                await _connectionManager.BroadcastToTopic("lobby", question);
                await Task.Delay(_gameTimeProvider.MilliSeconds);
            }
        }
    }
}