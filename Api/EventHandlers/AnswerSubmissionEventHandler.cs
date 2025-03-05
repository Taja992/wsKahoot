using Api.EventHandlers.Dtos;
using Api.WebSockets;
using EFScaffold;
using EFScaffold.EntityFramework;
using Fleck;
using Microsoft.EntityFrameworkCore;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class AnswerSubmissionEventHandler : BaseEventHandler<AnswerSubmissionDto>
    {
        private readonly IConnectionManager _connectionManager;
        private readonly KahootContext _context;
        private static Dictionary<string, Dictionary<string, int>> _playerScores = new();

        public AnswerSubmissionEventHandler(IConnectionManager connectionManager, KahootContext context)
        {
            _connectionManager = connectionManager;
            _context = context;
        }

        public override async Task Handle(AnswerSubmissionDto dto, IWebSocketConnection socket)
        {
            var clientId = await _connectionManager.GetClientIdFromSocketId(socket.ConnectionInfo.Id.ToString());

            // Find the question and selected option
            var question = await _context.Questions
                .Include(q => q.QuestionOptions)
                .FirstOrDefaultAsync(q => q.Id == dto.QuestionId);

            if (question == null)
            {
                socket.SendDto(new ServerSendsErrorMessageDto
                {
                    Error = "Question not found"
                });
                return;
            }

            // Find the selected option
            var selectedOption = question.QuestionOptions.FirstOrDefault(o => o.OptionText == dto.SelectedOption);
            if (selectedOption == null)
            {
                socket.SendDto(new ServerSendsErrorMessageDto
                {
                    Error = "Selected option not found"
                });
                return;
            }

            // Calculate score based on correctness and time remaining
            int score = 0;
            if (selectedOption.IsCorrect)
            {
                // Base score is 100, with bonus of up to 100 based on time remaining
                int timeBonus = dto.TimeRemaining.HasValue ? (dto.TimeRemaining.Value * 10) : 0;
                score = 100 + timeBonus;
            }

            // Store player's answer and score
            var player = await _context.Players.FirstOrDefaultAsync(p => p.Id == clientId);
            if (player == null)
            {
                // Create a temporary player if not in database
                player = new Player
                {
                    Id = clientId,
                    Nickname = clientId.Substring(0, 8), // Use part of client ID as nickname
                    GameId = question.GameId
                };
                _context.Players.Add(player);
                await _context.SaveChangesAsync();
            }

            // Record the answer
            var playerAnswer = new PlayerAnswer
            {
                PlayerId = player.Id,
                QuestionId = question.Id,
                SelectedOptionId = selectedOption.Id,
                AnswerTimestamp = DateTime.UtcNow
            };

            _context.PlayerAnswers.Add(playerAnswer);

            // Update player's score in memory
            if (!_playerScores.ContainsKey(question.GameId))
            {
                _playerScores[question.GameId] = new Dictionary<string, int>();
            }

            if (!_playerScores[question.GameId].ContainsKey(clientId))
            {
                _playerScores[question.GameId][clientId] = 0;
            }

            _playerScores[question.GameId][clientId] += score;

            await _context.SaveChangesAsync();

            // Send response to client
            socket.SendDto(new AnswerResponseDto
            {
                requestId = dto.requestId,
                IsCorrect = selectedOption.IsCorrect,
                Score = _playerScores[question.GameId][clientId]
            });
        }

        public static List<PlayerScoreDto> GetPlayerScores(string gameId)
        {
            if (!_playerScores.ContainsKey(gameId))
            {
                return new List<PlayerScoreDto>();
            }

            return _playerScores[gameId]
                .Select(kvp => new PlayerScoreDto { Id = kvp.Key, Score = kvp.Value })
                .ToList();
        }
    }
}