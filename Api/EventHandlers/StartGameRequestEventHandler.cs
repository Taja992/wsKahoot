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

            await BroadcastQuestionsSequentially(questions, dto.GameId);
        }

        private async Task BroadcastQuestionsSequentially(List<QuestionDto> questions, string gameId)
        {
            foreach (var question in questions)
            {
                // Broadcasting preparation message with correct eventType
                var prepareForQuestionDto = new PrepareForQuestionDto
                {
                    eventType = StringConstants.PrepareForQuestionDto,
                    SecondsUntilQuestion = 3
                };
                
                await _connectionManager.BroadcastToTopic($"game:{gameId}", prepareForQuestionDto);
                
                // Give clients time to prepare for the next question
                await Task.Delay(3000); // 3 seconds preparation time
                
                // Broadcasting the question with correct eventType
                question.eventType = StringConstants.QuestionDto;
                await _connectionManager.BroadcastToTopic($"game:{gameId}", question);
                
                // Wait the full question time from GameTimeProvider (10 seconds)
                await Task.Delay(_gameTimeProvider.MilliSeconds);
                
                // Send the time-up message with correct eventType
                var questionTimeUpDto = new QuestionTimeUpDto
                {
                    eventType = StringConstants.QuestionTimeUpDto,
                    QuestionId = question.Id
                };
                await _connectionManager.BroadcastToTopic($"game:{gameId}", questionTimeUpDto);
                
                // Mark the question as answered in the database
                var dbQuestion = await _context.Questions.FindAsync(question.Id);
                if (dbQuestion != null)
                {
                    dbQuestion.Answered = true;
                    await _context.SaveChangesAsync();
                }
                
                // Additional buffer for showing results before next question
                await Task.Delay(3000);
            }

            // After all questions, broadcast game complete with final scores
            var playerScores = await GetPlayerScores(gameId);

            var gameCompleteDto = new GameCompleteDto 
            { 
                eventType = StringConstants.GameCompleteDto,
                Players = playerScores
            };

            await _connectionManager.BroadcastToTopic($"game:{gameId}", gameCompleteDto);
        }
        
        private async Task<List<PlayerScoreDto>> GetPlayerScores(string gameId)
        {
            // Query to calculate player scores based on their correct answers
            var playerScores = await _context.Players
                .Where(p => p.GameId == gameId)
                .Select(p => new
                {
                    PlayerId = p.Id,
                    Nickname = p.Nickname,
                    // Count correct answers (join player_answer with question_option where is_correct is true)
                    CorrectAnswers = _context.PlayerAnswers
                        .Where(a => a.PlayerId == p.Id)
                        .Join(_context.QuestionOptions,
                            answer => answer.SelectedOptionId,
                            option => option.Id,
                            (answer, option) => option.IsCorrect)
                        .Count(isCorrect => isCorrect)
                })
                .ToListAsync();

            // Convert to PlayerScoreDto format
            return playerScores.Select(p => new PlayerScoreDto
            {
                Id = p.PlayerId,
                Nickname = p.Nickname,
                Score = p.CorrectAnswers
            }).ToList();
        }
    }
}