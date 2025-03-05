using Api.EventHandlers.Dtos;
using Api.Utilities;
using Api.WebSockets;
using Fleck;
using Microsoft.Extensions.Options;
using WebSocketBoilerplate;

namespace Api.EventHandlers
{
    public class AdminRequestEventHandler : BaseEventHandler<AdminRequestDto>
    {
        private static string? _adminClientId;
        private readonly string _adminPassword;
        private readonly IConnectionManager _connectionManager;

        public AdminRequestEventHandler(IConnectionManager connectionManager, IOptions<AppOptions> options)
        {
            _connectionManager = connectionManager;
            _adminPassword = options.Value.AdminPassword;
        }

        public override async Task Handle(AdminRequestDto dto, IWebSocketConnection socket)
        {
            Console.WriteLine($"Admin request received with password: {dto.Password}");

            var clientId = await _connectionManager.GetClientIdFromSocketId(socket.ConnectionInfo.Id.ToString());
            Console.WriteLine($"Client ID: {clientId}");

            if (dto.Password == _adminPassword)
            {
                _adminClientId = clientId;
                var response = new AdminResponseDto
                {
                    eventType = "AdminResponseDto",
                    requestId = dto.requestId,
                    IsAdmin = true,
                    Message = "You are now the admin."
                };
                Console.WriteLine($"Sending response: IsAdmin={response.IsAdmin}, Message={response.Message}");
                socket.SendDto(response);
            }
            else
            {
                var response = new AdminResponseDto
                {
                    eventType = "AdminResponseDto",
                    requestId = dto.requestId,
                    IsAdmin = false,
                    Message = "Invalid password."
                };
                Console.WriteLine($"Sending response: IsAdmin={response.IsAdmin}, Message={response.Message}");
                socket.SendDto(response);
            }
        }

        public static bool IsAdmin(string clientId)
        {
            return _adminClientId == clientId;
        }
    }
}