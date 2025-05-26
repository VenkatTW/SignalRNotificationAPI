# SignalR SQL Server Scaling Implementation

## Overview

This implementation provides a comprehensive SQL Server-based scaling solution for SignalR applications, designed to handle up to 500 concurrent users across multiple server instances with message persistence and reliability features.

## Architecture

```
[Load Balancer]
       |
   [Server 1] ←→ [SQL Server Database] ←→ [Server 2]
       |           (Shared State)           |
   [Clients]                          [Clients]
```

## Key Features

### 1. **SQL Server-Based Connection Management**
- Persistent connection tracking across server restarts
- Shared state between multiple server instances
- Automatic cleanup of stale connections
- Connection heartbeat monitoring

### 2. **Message Persistence & Reliability**
- All messages stored in SQL Server before sending
- Delivery confirmation tracking
- Offline message queuing
- Message expiration and cleanup
- Retry mechanism for failed deliveries

### 3. **Multi-Server Support**
- Horizontal scaling across multiple instances
- Load balancer compatibility
- Server instance identification
- Shared connection state

### 4. **Performance Optimizations**
- Connection pooling
- Batch heartbeat updates
- Indexed database queries
- Configurable cleanup intervals

## Database Schema

### Core Tables

#### UserConnections
Tracks active user connections across all server instances.

```sql
- Id (int, PK)
- UserId (nvarchar(100), indexed)
- ConnectionId (nvarchar(200), unique)
- ServerInstance (nvarchar(50))
- ConnectedAt (datetime2)
- LastHeartbeat (datetime2, indexed)
- IsActive (bit, indexed)
```

#### PersistedMessages
Stores all messages with delivery tracking.

```sql
- Id (int, PK)
- TargetUserId (nvarchar(100), indexed)
- Message (nvarchar(max))
- SenderUserId (nvarchar(100), nullable)
- MessageType (nvarchar(50))
- CreatedAt (datetime2, indexed)
- DeliveredAt (datetime2, nullable)
- IsDelivered (bit, indexed)
- IsPersistent (bit)
- ExpiresAt (datetime2, indexed)
- Metadata (nvarchar(500), nullable)
```

#### MessageDeliveryStatus
Tracks delivery attempts and success/failure.

```sql
- Id (int, PK)
- MessageId (int, FK to PersistedMessages)
- ConnectionId (nvarchar(200))
- AttemptedAt (datetime2)
- IsSuccessful (bit)
- ErrorMessage (nvarchar(500), nullable)
```

#### UserSessions
Manages user session information.

```sql
- Id (int, PK)
- UserId (nvarchar(100), indexed)
- SessionStart (datetime2)
- SessionEnd (datetime2, nullable)
- IsActive (bit, indexed)
- ServerInstance (nvarchar(50))
- ConnectionCount (int)
- LastActivity (datetime2, indexed)
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SignalRNotificationDB;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "SignalRSettings": {
    "ConnectionTimeoutMinutes": 5,
    "HeartbeatIntervalSeconds": 30,
    "MessageRetentionDays": 7,
    "CleanupIntervalMinutes": 15,
    "MaxConcurrentConnections": 500
  }
}
```

## Services

### IConnectionManager
Manages user connections across server instances.

**Key Methods:**
- `AddConnectionAsync(userId, connectionId)` - Register new connection
- `RemoveConnectionAsync(connectionId)` - Remove connection
- `GetConnectionIdsForUserAsync(userId)` - Get active connections for user
- `IsUserOnlineAsync(userId)` - Check if user is online
- `UpdateHeartbeatAsync(connectionId)` - Update connection heartbeat
- `CleanupStaleConnectionsAsync()` - Remove inactive connections

### IMessagePersistenceService
Handles message storage and delivery tracking.

**Key Methods:**
- `SaveMessageAsync(targetUserId, message, ...)` - Store message
- `MarkMessageAsDeliveredAsync(messageId, connectionId)` - Mark as delivered
- `GetUndeliveredMessagesForUserAsync(userId)` - Get pending messages
- `CleanupExpiredMessagesAsync()` - Remove expired messages
- `RecordDeliveryAttemptAsync(...)` - Log delivery attempts

## Usage Examples

### Sending a Notification

```csharp
public async Task SendNotification(string targetUser, string message)
{
    // Save message to database first for reliability
    var messageId = await _messagePersistence.SaveMessageAsync(targetUser, message, "System");
    
    // Get active connections for the target user
    var connectionIds = await _connectionManager.GetConnectionIdsForUserAsync(targetUser);
    
    if (connectionIds.Any())
    {
        // User is online - send message immediately
        await Clients.Clients(connectionIds).SendAsync("ReceiveNotification", targetUser, message);
        
        // Mark message as delivered
        foreach (var connectionId in connectionIds)
        {
            await _messagePersistence.MarkMessageAsDeliveredAsync(messageId, connectionId);
        }
    }
    // If user is offline, message will be delivered when they reconnect
}
```

### Connection Management

```csharp
public override async Task OnConnectedAsync()
{
    var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
    if (!string.IsNullOrEmpty(userId))
    {
        await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);
        
        // Deliver any pending messages
        var pendingMessages = await _messagePersistence.GetUndeliveredMessagesForUserAsync(userId);
        foreach (var message in pendingMessages)
        {
            await Clients.Caller.SendAsync("ReceiveNotification", userId, message.Message);
            await _messagePersistence.MarkMessageAsDeliveredAsync(message.Id, Context.ConnectionId);
        }
    }
    
    await base.OnConnectedAsync();
}
```

## Deployment Guide

### 1. Database Setup

Update your connection string in `appsettings.json` to point to your SQL Server instance:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=SignalRNotificationDB;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### 2. Database Migration

Run Entity Framework migrations to create the database schema:

```bash
dotnet ef migrations add InitialSignalRScaling
dotnet ef database update
```

### 3. Multiple Server Deployment

Deploy multiple instances of the application behind a load balancer:

```
[Load Balancer:80] 
    ↓
[Server 1:5001] [Server 2:5002] [Server 3:5003]
    ↓               ↓               ↓
[SQL Server Database - Shared State]
```

### 4. Load Balancer Configuration

Configure your load balancer for SignalR:
- Enable sticky sessions for WebSocket connections
- Use round-robin for HTTP requests
- Health check endpoint: `/health` (if implemented)

## Performance Considerations

### For 500 Concurrent Users

1. **Database Optimization:**
   - Ensure proper indexing on frequently queried columns
   - Configure connection pooling (recommended: 50-100 connections)
   - Regular maintenance and statistics updates

2. **Memory Management:**
   - Each connection uses ~8KB memory
   - 500 connections ≈ 4MB base memory usage
   - Message caching adds additional memory overhead

3. **Network Optimization:**
   - Use compression for large messages
   - Implement message batching where possible
   - Configure appropriate keep-alive intervals

4. **Cleanup Operations:**
   - Stale connection cleanup every 15 minutes
   - Message expiration after 7 days (configurable)
   - Automatic heartbeat batching

## Monitoring and Logging

### Key Metrics to Monitor

1. **Connection Metrics:**
   - Active connection count
   - Connection establishment rate
   - Connection failure rate

2. **Message Metrics:**
   - Message delivery success rate
   - Average delivery time
   - Pending message queue size

3. **Database Metrics:**
   - Query execution time
   - Connection pool usage
   - Database size growth

### Logging Configuration

The implementation includes comprehensive logging:
- Connection events (connect/disconnect)
- Message delivery status
- Error conditions
- Performance metrics

## Troubleshooting

### Common Issues

1. **High Memory Usage:**
   - Check for connection leaks
   - Verify cleanup service is running
   - Monitor message queue size

2. **Slow Message Delivery:**
   - Check database performance
   - Verify network connectivity
   - Review connection pool settings

3. **Connection Drops:**
   - Verify heartbeat configuration
   - Check load balancer settings
   - Review firewall rules

### Health Checks

Implement health checks to monitor system status:
- Database connectivity
- Active connection count
- Message queue health
- Background service status

## Security Considerations

1. **Database Security:**
   - Use encrypted connections (TrustServerCertificate=true for dev only)
   - Implement proper authentication
   - Regular security updates

2. **Connection Security:**
   - Validate user authentication
   - Implement rate limiting
   - Monitor for suspicious activity

3. **Message Security:**
   - Sanitize message content
   - Implement message size limits
   - Consider message encryption for sensitive data

## Conclusion

This SQL Server-based scaling solution provides a robust, reliable, and scalable foundation for SignalR applications. It combines the familiarity of SQL Server with advanced features like message persistence, multi-server support, and comprehensive monitoring.

The implementation is designed to handle your requirement of 500 concurrent users while providing room for future growth and the reliability features you need for production use.
