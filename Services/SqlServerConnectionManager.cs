using Microsoft.EntityFrameworkCore;
using SignalRNotificationAPI.Data;
using SignalRNotificationAPI.Models;
using System.Collections.Concurrent;

namespace SignalRNotificationAPI.Services
{
    public interface IConnectionManager
    {
        Task AddConnectionAsync(string userId, string connectionId);
        Task RemoveConnectionAsync(string connectionId);
        Task<IEnumerable<string>> GetConnectionIdsForUserAsync(string userId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task UpdateHeartbeatAsync(string connectionId);
        Task CleanupStaleConnectionsAsync();
        Task<int> GetActiveConnectionCountAsync();
    }

    public interface IMessagePersistenceService
    {
        Task<int> SaveMessageAsync(string targetUserId, string message, string? senderUserId = null, string messageType = "Notification", bool isPersistent = true, DateTime? expiresAt = null);
        Task MarkMessageAsDeliveredAsync(int messageId, string connectionId);
        Task<IEnumerable<PersistedMessage>> GetUndeliveredMessagesForUserAsync(string userId);
        Task CleanupExpiredMessagesAsync();
        Task<bool> RecordDeliveryAttemptAsync(int messageId, string connectionId, bool isSuccessful, string? errorMessage = null);
    }

    public class SqlServerConnectionManager : IConnectionManager, IMessagePersistenceService
    {
        private readonly SignalRDbContext _context;
        private readonly ILogger<SqlServerConnectionManager> _logger;
        private readonly string _serverInstance;
        private readonly ConcurrentDictionary<string, DateTime> _heartbeatCache;

        public SqlServerConnectionManager(SignalRDbContext context, ILogger<SqlServerConnectionManager> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _serverInstance = Environment.MachineName + "_" + Environment.ProcessId;
            _heartbeatCache = new ConcurrentDictionary<string, DateTime>();
        }

        #region Connection Management

        public async Task AddConnectionAsync(string userId, string connectionId)
        {
            try
            {
                // Check if connection already exists
                var existingConnection = await _context.UserConnections
                    .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

                if (existingConnection != null)
                {
                    // Update existing connection
                    existingConnection.UserId = userId;
                    existingConnection.ServerInstance = _serverInstance;
                    existingConnection.LastHeartbeat = DateTime.UtcNow;
                    existingConnection.IsActive = true;

                    _logger.LogInformation("Updated existing connection {ConnectionId} for user {UserId} on server {ServerInstance}",
                        connectionId, userId, _serverInstance);
                }
                else
                {
                    // Create new connection
                    var connection = new UserConnection
                    {
                        UserId = userId,
                        ConnectionId = connectionId,
                        ServerInstance = _serverInstance,
                        ConnectedAt = DateTime.UtcNow,
                        LastHeartbeat = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.UserConnections.Add(connection);

                    _logger.LogInformation("User {UserId} connected with new connection {ConnectionId} on server {ServerInstance}",
                        userId, connectionId, _serverInstance);
                }

                await _context.SaveChangesAsync();

                // Update or create user session
                await UpdateUserSessionAsync(userId, true);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "DbContext was disposed while adding connection for user {UserId}", userId);
                throw new InvalidOperationException("Database context is no longer available. Please retry the operation.", ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true)
            {
                // Handle race condition where another thread inserted the same ConnectionId
                _logger.LogWarning("Duplicate connection ID {ConnectionId} detected for user {UserId}. Attempting to update existing record.",
                    connectionId, userId);

                try
                {
                    // Try to update the existing record
                    var existingConnection = await _context.UserConnections
                        .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

                    if (existingConnection != null)
                    {
                        existingConnection.UserId = userId;
                        existingConnection.ServerInstance = _serverInstance;
                        existingConnection.LastHeartbeat = DateTime.UtcNow;
                        existingConnection.IsActive = true;
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Successfully updated duplicate connection {ConnectionId} for user {UserId}",
                            connectionId, userId);
                    }
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update duplicate connection {ConnectionId} for user {UserId}",
                        connectionId, userId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding connection for user {UserId} with connection {ConnectionId}", userId, connectionId);
                throw;
            }
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            try
            {
                var connection = await _context.UserConnections
                    .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);

                if (connection != null)
                {
                    _context.UserConnections.Remove(connection);
                    await _context.SaveChangesAsync();

                    // Update user session if no more active connections
                    var hasActiveConnections = await _context.UserConnections
                        .AnyAsync(c => c.UserId == connection.UserId && c.IsActive);

                    if (!hasActiveConnections)
                    {
                        await UpdateUserSessionAsync(connection.UserId, false);
                    }

                    _heartbeatCache.TryRemove(connectionId, out _);

                    _logger.LogInformation("Connection {ConnectionId} removed for user {UserId}",
                        connectionId, connection.UserId);
                }
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "DbContext was disposed while removing connection {ConnectionId}", connectionId);
                throw new InvalidOperationException("Database context is no longer available. Please retry the operation.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetConnectionIdsForUserAsync(string userId)
        {
            try
            {
                return await _context.UserConnections
                    .Where(c => c.UserId == userId && c.IsActive)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection IDs for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<bool> IsUserOnlineAsync(string userId)
        {
            try
            {
                return await _context.UserConnections
                    .AnyAsync(c => c.UserId == userId && c.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
                return false;
            }
        }

        public async Task UpdateHeartbeatAsync(string connectionId)
        {
            try
            {
                // Use cache to reduce database hits
                _heartbeatCache.AddOrUpdate(connectionId, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);

                // Batch update heartbeats every 30 seconds
                if (_heartbeatCache.Count > 10 || DateTime.UtcNow.Second % 30 == 0)
                {
                    await FlushHeartbeatsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating heartbeat for connection {ConnectionId}", connectionId);
            }
        }

        public async Task CleanupStaleConnectionsAsync()
        {
            try
            {
                var staleThreshold = DateTime.UtcNow.AddMinutes(-5);
                var staleConnections = await _context.UserConnections
                    .Where(c => c.LastHeartbeat < staleThreshold && c.IsActive)
                    .ToListAsync();

                foreach (var connection in staleConnections)
                {
                    connection.IsActive = false;
                }

                if (staleConnections.Any())
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} stale connections", staleConnections.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up stale connections");
            }
        }

        public async Task<int> GetActiveConnectionCountAsync()
        {
            try
            {
                return await _context.UserConnections.CountAsync(c => c.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active connection count");
                return 0;
            }
        }

        #endregion

        #region Message Persistence

        public async Task<int> SaveMessageAsync(string targetUserId, string message, string? senderUserId = null,
            string messageType = "Notification", bool isPersistent = true, DateTime? expiresAt = null)
        {
            try
            {
                var persistedMessage = new PersistedMessage
                {
                    TargetUserId = targetUserId,
                    Message = message,
                    SenderUserId = senderUserId,
                    MessageType = messageType,
                    CreatedAt = DateTime.UtcNow,
                    IsDelivered = false,
                    IsPersistent = isPersistent,
                    ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7) // Default 7 days expiry
                };

                _context.PersistedMessages.Add(persistedMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message saved for user {UserId} with ID {MessageId}",
                    targetUserId, persistedMessage.Id);

                return persistedMessage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving message for user {UserId}", targetUserId);
                throw;
            }
        }

        public async Task MarkMessageAsDeliveredAsync(int messageId, string connectionId)
        {
            try
            {
                var message = await _context.PersistedMessages.FindAsync(messageId);
                if (message != null && !message.IsDelivered)
                {
                    message.IsDelivered = true;
                    message.DeliveredAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await RecordDeliveryAttemptAsync(messageId, connectionId, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message {MessageId} as delivered", messageId);
            }
        }

        public async Task<IEnumerable<PersistedMessage>> GetUndeliveredMessagesForUserAsync(string userId)
        {
            try
            {
                return await _context.PersistedMessages
                    .Where(m => m.TargetUserId == userId &&
                               !m.IsDelivered &&
                               (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow))
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting undelivered messages for user {UserId}", userId);
                return new List<PersistedMessage>();
            }
        }

        public async Task CleanupExpiredMessagesAsync()
        {
            try
            {
                var expiredMessages = await _context.PersistedMessages
                    .Where(m => m.ExpiresAt != null && m.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                if (expiredMessages.Any())
                {
                    _context.PersistedMessages.RemoveRange(expiredMessages);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} expired messages", expiredMessages.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired messages");
            }
        }

        public async Task<bool> RecordDeliveryAttemptAsync(int messageId, string connectionId, bool isSuccessful, string? errorMessage = null)
        {
            try
            {
                var deliveryStatus = new MessageDeliveryStatus
                {
                    MessageId = messageId,
                    ConnectionId = connectionId,
                    AttemptedAt = DateTime.UtcNow,
                    IsSuccessful = isSuccessful,
                    ErrorMessage = errorMessage
                };

                _context.MessageDeliveryStatuses.Add(deliveryStatus);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording delivery attempt for message {MessageId}", messageId);
                return false;
            }
        }

        #endregion

        #region Private Methods

        private async Task UpdateUserSessionAsync(string userId, bool isConnecting)
        {
            try
            {
                var activeSession = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

                if (isConnecting)
                {
                    if (activeSession == null)
                    {
                        var newSession = new UserSession
                        {
                            UserId = userId,
                            SessionStart = DateTime.UtcNow,
                            IsActive = true,
                            ServerInstance = _serverInstance,
                            ConnectionCount = 1,
                            LastActivity = DateTime.UtcNow
                        };
                        _context.UserSessions.Add(newSession);
                    }
                    else
                    {
                        activeSession.ConnectionCount++;
                        activeSession.LastActivity = DateTime.UtcNow;
                    }
                }
                else
                {
                    if (activeSession != null)
                    {
                        activeSession.IsActive = false;
                        activeSession.SessionEnd = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user session for {UserId}", userId);
            }
        }

        private async Task FlushHeartbeatsAsync()
        {
            try
            {
                var heartbeatsToUpdate = _heartbeatCache.ToList();
                _heartbeatCache.Clear();

                if (heartbeatsToUpdate.Any())
                {
                    var connectionIds = heartbeatsToUpdate.Select(h => h.Key).ToList();
                    var connections = await _context.UserConnections
                        .Where(c => connectionIds.Contains(c.ConnectionId))
                        .ToListAsync();

                    foreach (var connection in connections)
                    {
                        if (heartbeatsToUpdate.FirstOrDefault(h => h.Key == connection.ConnectionId) is var heartbeat &&
                            !heartbeat.Equals(default(KeyValuePair<string, DateTime>)))
                        {
                            connection.LastHeartbeat = heartbeat.Value;
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing heartbeats");
            }
        }

        #endregion
    }
}
