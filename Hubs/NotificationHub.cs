using Microsoft.AspNetCore.SignalR;
using SignalRNotificationAPI.Services;

namespace SignalRNotificationAPI.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IMessagePersistenceService _messagePersistence;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(IConnectionManager connectionManager, IMessagePersistenceService messagePersistence, ILogger<NotificationHub> logger)
        {
            _connectionManager = connectionManager;
            _messagePersistence = messagePersistence;
            _logger = logger;
        }

        // Method to register a user with their connection ID
        public async Task RegisterUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;

            try
            {
                await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);

                // Send any undelivered messages to the user
                await DeliverPendingMessagesAsync(userId);

                await Clients.Caller.SendAsync("ReceiveNotification", userId, $"Registered as {userId}");

                _logger.LogInformation("User {UserId} registered with connection {ConnectionId}", userId, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user {UserId}", userId);
                await Clients.Caller.SendAsync("ReceiveError", "Registration failed");
            }
        }

        // Method to send notification to a specific user with persistence
        public async Task SendNotification(string targetUser, string message, string? senderUserId = null)
        {
            if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message))
                return;

            try
            {
                // Save message to database first for reliability
                var messageId = await _messagePersistence.SaveMessageAsync(targetUser, message, senderUserId);

                // Get active connections for the target user
                var connectionIds = await _connectionManager.GetConnectionIdsForUserAsync(targetUser);

                if (connectionIds.Any())
                {
                    // User is online - send message immediately
                    var connectionIdsList = connectionIds.ToList();
                    await Clients.Clients(connectionIdsList).SendAsync("ReceiveNotification", targetUser, message);

                    // Mark message as delivered for each connection
                    foreach (var connectionId in connectionIdsList)
                    {
                        await _messagePersistence.MarkMessageAsDeliveredAsync(messageId, connectionId);
                    }

                    _logger.LogInformation("Message {MessageId} delivered to {ConnectionCount} connections for user {UserId}",
                        messageId, connectionIdsList.Count, targetUser);
                }
                else
                {
                    // User is offline - message will be delivered when they reconnect
                    _logger.LogInformation("Message {MessageId} queued for offline user {UserId}", messageId, targetUser);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", targetUser);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to send notification");
            }
        }

        // Method to send broadcast message to all connected users
        public async Task SendBroadcast(string message, string? senderUserId = null)
        {
            try
            {
                await Clients.All.SendAsync("ReceiveBroadcast", message);
                _logger.LogInformation("Broadcast message sent by {SenderUserId}", senderUserId ?? "System");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending broadcast message");
                await Clients.Caller.SendAsync("ReceiveError", "Failed to send broadcast");
            }
        }

        // Method to check if a user is online
        public async Task<bool> IsUserOnline(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                return await _connectionManager.IsUserOnlineAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
                return false;
            }
        }

        // Method to get active connection count
        public async Task<int> GetActiveConnectionCount()
        {
            try
            {
                return await _connectionManager.GetActiveConnectionCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active connection count");
                return 0;
            }
        }

        // Override OnConnectedAsync to extract username from query parameters
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                var userId = httpContext.Request.Query["userId"].ToString();
                if (!string.IsNullOrEmpty(userId))
                {
                    _logger.LogInformation("User {UserId} connected with connection ID: {ConnectionId}", userId, Context.ConnectionId);

                    // Register the connection directly without calling RegisterUser to avoid double registration
                    try
                    {
                        await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);

                        // Send any undelivered messages to the user
                        await DeliverPendingMessagesAsync(userId);

                        _logger.LogInformation("User {UserId} auto-registered on connection", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error auto-registering user {UserId} on connection", userId);
                    }
                }
                else
                {
                    _logger.LogWarning("Connection {ConnectionId} established without userId", Context.ConnectionId);
                }
            }

            await base.OnConnectedAsync();
        }

        // Override OnDisconnectedAsync to clean up connections
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

                if (exception != null)
                {
                    _logger.LogWarning(exception, "Connection {ConnectionId} disconnected with exception", Context.ConnectionId);
                }
                else
                {
                    _logger.LogInformation("Connection {ConnectionId} disconnected normally", Context.ConnectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup for {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to update heartbeat for connection monitoring
        public async Task UpdateHeartbeat()
        {
            try
            {
                await _connectionManager.UpdateHeartbeatAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating heartbeat for connection {ConnectionId}", Context.ConnectionId);
            }
        }

        // Private method to deliver pending messages when user connects
        private async Task DeliverPendingMessagesAsync(string userId)
        {
            try
            {
                var pendingMessages = await _messagePersistence.GetUndeliveredMessagesForUserAsync(userId);

                foreach (var message in pendingMessages)
                {
                    await Clients.Caller.SendAsync("ReceiveNotification", userId, message.Message);
                    await _messagePersistence.MarkMessageAsDeliveredAsync(message.Id, Context.ConnectionId);
                }

                if (pendingMessages.Any())
                {
                    _logger.LogInformation("Delivered {MessageCount} pending messages to user {UserId}",
                        pendingMessages.Count(), userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delivering pending messages to user {UserId}", userId);
            }
        }

        // Static method for backward compatibility (can be used by controllers)
        public static async Task<IEnumerable<string>> GetConnectionIdsForUser(string userId, IConnectionManager connectionManager)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<string>();

            return await connectionManager.GetConnectionIdsForUserAsync(userId);
        }
    }
}
