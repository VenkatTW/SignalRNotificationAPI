using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalRNotificationAPI.WebSockets
{
    public class WebSocketHandler
    {
        // Dictionary to store user connections (userId -> WebSocket)
        private static readonly ConcurrentDictionary<string, WebSocket> UserSockets = new ConcurrentDictionary<string, WebSocket>();

        // Method to handle new WebSocket connections
        public static async Task OnConnected(WebSocket socket, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                await CloseSocketAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "User ID is required");
                return;
            }

            // Add the WebSocket to the dictionary
            UserSockets.TryAdd(userId, socket);
            Console.WriteLine($"WebSocket connected for user {userId}");

            // Send a welcome message
            await SendMessageAsync(userId, $"Registered as {userId} via WebSocket");

            // Start receiving messages
            await ReceiveMessagesAsync(socket, userId);
        }

        // Method to receive messages from the WebSocket
        private static async Task ReceiveMessagesAsync(WebSocket socket, string userId)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = null;

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var messageBuilder = new StringBuilder();
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var messageChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            messageBuilder.Append(messageChunk);
                        }
                        else if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await OnDisconnected(userId);
                            return;
                        }
                    }
                    while (!result.EndOfMessage);

                    var message = messageBuilder.ToString();
                    Console.WriteLine($"Received message from user {userId}: {message}");

                    // Process the message (could implement command handling here)
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving WebSocket message: {ex.Message}");
            }
            finally
            {
                await OnDisconnected(userId);
            }
        }

        // Method to send a message to a specific user
        public static async Task SendMessageAsync(string userId, string message)
        {
            if (string.IsNullOrEmpty(userId) || !UserSockets.TryGetValue(userId, out var socket))
            {
                return;
            }

            if (socket.State != WebSocketState.Open)
            {
                await OnDisconnected(userId);
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length),
                    WebSocketMessageType.Text, true, CancellationToken.None);

                Console.WriteLine($"Sent WebSocket message to user {userId}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending WebSocket message: {ex.Message}");
                await OnDisconnected(userId);
            }
        }

        // Method to handle disconnections
        private static async Task OnDisconnected(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Remove the WebSocket from the dictionary
            if (UserSockets.TryRemove(userId, out var socket))
            {
                Console.WriteLine($"WebSocket disconnected for user {userId}");

                if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
                {
                    await CloseSocketAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed");
                }
            }
        }

        // Helper method to close a WebSocket
        private static async Task CloseSocketAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
        {
            try
            {
                if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
                {
                    await socket.CloseAsync(status, reason, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
        }

        // Method to get all connected user IDs
        public static IEnumerable<string> GetConnectedUserIds()
        {
            return UserSockets.Keys;
        }

        // Method to check if a user is connected
        public static bool IsUserConnected(string userId)
        {
            return !string.IsNullOrEmpty(userId) && UserSockets.ContainsKey(userId);
        }
    }
}
