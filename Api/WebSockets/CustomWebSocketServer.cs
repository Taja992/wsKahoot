using System.Net;
using System.Net.Sockets;
using System.Web;
using Api.EventHandlers.Dtos;
using Fleck;
using WebSocketBoilerplate;

namespace Api.WebSockets;

public class CustomWebSocketServer(IConnectionManager manager, ILogger<CustomWebSocketServer> logger)
{
    public void Start(WebApplication app)
    {
        var port = GetAvailablePort(8080);
        Environment.SetEnvironmentVariable("PORT", port.ToString());
        var url = $"ws://0.0.0.0:{port}";
        var server = new WebSocketServer(url);

        server.Start(socket =>
        {
            var queryString = socket.ConnectionInfo.Path.Split('?').Length > 1
                ? socket.ConnectionInfo.Path.Split('?')[1]
                : "";

            var id = HttpUtility.ParseQueryString(queryString)["id"];

            socket.OnOpen = () => manager.OnOpen(socket, id);
            socket.OnClose = () => manager.OnClose(socket, id);
            socket.OnMessage = message =>
            {
                Console.WriteLine($"Received message: {message}");
                Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine("About to call event handler");
                        await app.CallEventHandler(socket, message);
                        Console.WriteLine("Event handler completed");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error in event handler: {e.Message}");
                        Console.WriteLine($"Stack trace: {e.StackTrace}");
                        logger.LogError(e, "Error while handling message");
                        try
                        {
                            socket.SendDto(new ServerSendsErrorMessageDto
                            {
                                Error = e.Message
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error sending error message: {ex.Message}");
                        }
                    }
                });
            };
        });
    }

    /// <summary>
    ///     This is relevant for testing since concurrent tests require ports to serve the websocket server on
    /// </summary>
    /// <param name="startPort"></param>
    /// <returns></returns>
    private int GetAvailablePort(int startPort)
    {
        var port = startPort;
        var isPortAvailable = false;

        do
        {
            try
            {
                var tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                isPortAvailable = true;
            }
            catch (SocketException)
            {
                port++;
            }
        } while (!isPortAvailable);

        return port;
    }
}