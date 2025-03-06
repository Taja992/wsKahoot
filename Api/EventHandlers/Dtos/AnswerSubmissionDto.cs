using WebSocketBoilerplate;

namespace Api.EventHandlers.Dtos
{
    public class AnswerSubmissionDto : BaseDto
    {
        public string QuestionId { get; set; }
        public string SelectedOption { get; set; }
        public int? TimeRemaining { get; set; }
    }

    public class AnswerResponseDto : BaseDto
    {
        public bool IsCorrect { get; set; }
        public int Score { get; set; }
    }

    public class GameCompleteDto : BaseDto
    {
        public List<PlayerScoreDto> Players { get; set; } = new List<PlayerScoreDto>();
    }

    public class PlayerScoreDto
    {
        public string Id { get; set; }
        public string Nickname { get; set; }
        public int Score { get; set; }
    }
}