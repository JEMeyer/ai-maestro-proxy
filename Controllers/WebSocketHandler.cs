// Controllers/WebSocketHandler.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using AIMaestroProxy.Models;
using AIMaestroProxy.Interfaces;

namespace AIMaestroProxy.Controllers
{
    public class WebSocketHandler(IGpuManagerService gpuManagerService, ILogger<WebSocketHandler> logger)
    {
        private readonly IGpuManagerService _gpuManagerService = gpuManagerService;
        private readonly ILogger<WebSocketHandler> _logger = logger;

        // Store GPU reservations associated with WebSocket connections
        private readonly ConcurrentDictionary<WebSocket, List<string>> _gpuReservations = new();
        private readonly ConcurrentDictionary<WebSocket, bool> _heartbeatTracker = new();

        private const int KeepAliveInterval = 30000; // 30 seconds

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            _logger.LogInformation("Client connected.");

            _heartbeatTracker[webSocket] = true;

            var receiveBuffer = new byte[1024 * 4];
            _ = KeepAliveAsync(webSocket);

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client initiated close.");
                        break;
                    }

                    var message = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    await ProcessMessageAsync(webSocket, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket error.");
            }
            finally
            {
                // Cleanup on disconnect
                await CleanupConnectionAsync(webSocket);
            }
        }

        private async Task ProcessMessageAsync(WebSocket webSocket, string message)
        {
            try
            {
                var webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(message);
                if (webSocketMessage == null || string.IsNullOrEmpty(webSocketMessage.Command))
                {
                    await SendErrorAsync(webSocket, "Invalid message format.");
                    return;
                }

                switch (webSocketMessage.Command.ToLower())
                {
                    case "reserve":
                        ArgumentException.ThrowIfNullOrEmpty(webSocketMessage.ModelName);
                        ArgumentNullException.ThrowIfNull(webSocketMessage.OutputType);
                        await HandleReserveCommandAsync(webSocket, webSocketMessage.ModelName, (PathCategories.OutputType)webSocketMessage.OutputType);
                        break;

                    case "release":
                        await HandleReleaseCommandAsync(webSocket, webSocketMessage.GpuIds);
                        break;

                    case "pong":
                        ArgumentNullException.ThrowIfNull(webSocketMessage.GpuIds);
                        _heartbeatTracker[webSocket] = true;
                        _gpuManagerService.RefreshGpuActivity(webSocketMessage.GpuIds);
                        _logger.LogInformation("Received pong from client.");
                        break;

                    default:
                        await SendErrorAsync(webSocket, $"Unknown command: {webSocketMessage.Command}");
                        break;
                }
            }
            catch (JsonException)
            {
                await SendErrorAsync(webSocket, "Invalid JSON format.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message.");
                await SendErrorAsync(webSocket, "Internal server error.");
            }
        }

        private async Task HandleReserveCommandAsync(WebSocket webSocket, string modelName, PathCategories.OutputType outputType)
        {
            try
            {
                var modelAssignment = await _gpuManagerService.GetAvailableModelAssignmentAsync((PathCategories.OutputType)outputType, modelName, CancellationToken.None);
                if (modelAssignment == null)
                {
                    await SendErrorAsync(webSocket, "No suitable GPU available for the requested model.");
                    return;
                }

                // Extract GPU IDs from the model assignment
                var gpuIds = modelAssignment.GpuIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToArray();

                // Add the GPU reservation
                _gpuReservations.AddOrUpdate(webSocket,
                    [.. gpuIds],
                    (key, existingList) =>
                    {
                        existingList.AddRange(gpuIds);
                        return existingList;
                    });

                _logger.LogInformation("Reserved GPU(s): {Gpus}.", string.Join(", ", gpuIds));

                // Send response back to client
                var response = new WebSocketResponse
                {
                    Status = "success",
                    Host = modelAssignment.Ip,
                    Port = modelAssignment.Port,
                    GpuIds = gpuIds
                };

                await SendResponseAsync(webSocket, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reserving GPU(s).");
                await SendErrorAsync(webSocket, "Error reserving GPU(s).");
            }
        }

        private async Task HandleReleaseCommandAsync(WebSocket webSocket, string[]? gpuIds)
        {
            if (gpuIds == null)
            {
                await SendErrorAsync(webSocket, "GpuIds cannot be null for release command.");
                return;
            }

            try
            {
                _gpuManagerService.UnlockGPUs(gpuIds);

                _logger.LogInformation("Released GPU(s): {Gpus}.", string.Join(", ", gpuIds));

                // Send response back to client
                var response = new WebSocketResponse
                {
                    Status = "success",
                };

                await SendResponseAsync(webSocket, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing GPU(s).");
                await SendErrorAsync(webSocket, "Error releasing GPU(s).");
            }
        }

        private static async Task SendResponseAsync(WebSocket webSocket, WebSocketResponse response)
        {
            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            var buffer = new ArraySegment<byte>(responseBytes);

            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task SendErrorAsync(WebSocket webSocket, string errorMessage)
        {
            var response = new WebSocketResponse
            {
                Status = "error",
                Message = errorMessage
            };

            await SendResponseAsync(webSocket, response);
        }

        private async Task KeepAliveAsync(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    if (!_heartbeatTracker.TryGetValue(webSocket, out bool isAlive) || !isAlive)
                    {
                        _logger.LogWarning("Client unresponsive. Terminating connection.");
                        await CleanupConnectionAsync(webSocket);
                        break;
                    }

                    _heartbeatTracker[webSocket] = false;
                    var pingMessage = Encoding.UTF8.GetBytes("ping");
                    await webSocket.SendAsync(new ArraySegment<byte>(pingMessage), WebSocketMessageType.Text, true, CancellationToken.None);

                    await Task.Delay(KeepAliveInterval);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KeepAlive error.");
                    await CleanupConnectionAsync(webSocket);
                    break;
                }
            }
        }

        private async Task CleanupConnectionAsync(WebSocket webSocket)
        {
            if (_gpuReservations.TryRemove(webSocket, out var gpuList))
            {
                foreach (var gpuId in gpuList)
                {
                    _gpuManagerService.UnlockGPUs([gpuId]);
                }
            }

            _heartbeatTracker.TryRemove(webSocket, out _);

            if (webSocket.State != WebSocketState.Closed)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }

            _logger.LogInformation("Client disconnected and cleaned up.");
        }
    }
}
