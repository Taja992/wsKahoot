using WebSocketBoilerplate;

namespace Api.EventHandlers.Dtos;

public class JoinGameRequestDto : BaseDto
{
    public string GameId { get; set; }
    public string Nickname { get; set; }
}

public class JoinGameResponseDto : BaseDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class GamePlayersUpdateDto : BaseDto
{
    public string GameId { get; set; }
    public List<GamePlayerDto> Players { get; set; } = new List<GamePlayerDto>();
}

public class GamePlayerDto
{
    public string Id { get; set; }
    public string Nickname { get; set; }
}