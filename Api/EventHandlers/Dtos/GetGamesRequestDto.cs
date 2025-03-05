using System.Collections.Generic;
using WebSocketBoilerplate;

namespace Api.EventHandlers.Dtos;

    public class GetGamesRequestDto : BaseDto
    {
    }

    public class GetGamesResponseDto : BaseDto
    {
        public List<GameDto> Games { get; set; } = new List<GameDto>();
    }

    public class GameDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
