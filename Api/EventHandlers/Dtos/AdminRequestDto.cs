using System.Text.Json.Serialization;
using WebSocketBoilerplate;

namespace Api.EventHandlers.Dtos;


    public class AdminRequestDto : BaseDto
    {
        public string Password { get; set; }
    }

    public class AdminResponseDto : BaseDto
    {
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }
    
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
