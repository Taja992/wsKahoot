using WebSocketBoilerplate;

namespace Api.EventHandlers.Dtos
{
    public class StartGameRequestDto : BaseDto
    {
        public string GameId { get; set; }
    }

    public class StartGameResponseDto : BaseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class QuestionDto : BaseDto
    {
        public string Id { get; set; }
        public string QuestionText { get; set; }
        public List<QuestionOptionDto> Options { get; set; }
    }

    public class QuestionOptionDto
    {
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
    }
}