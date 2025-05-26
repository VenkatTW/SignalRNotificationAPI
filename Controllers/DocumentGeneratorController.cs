using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SignalRNotificationAPI.WebSockets;
using SignalRNotificationAPI.Hubs;
using SignalRNotificationAPI.Services;

[ApiController]
[Route("api/[controller]")]
public class DocumentGeneratorController : ControllerBase
{
  private readonly IHubContext<NotificationHub> _hubContext;
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<DocumentGeneratorController> _logger;

  public DocumentGeneratorController(
    IHubContext<NotificationHub> hubContext,
    IServiceProvider serviceProvider,
    ILogger<DocumentGeneratorController> logger)
  {
    _hubContext = hubContext;
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  public class DocumentGenerationRequest
  {
    public string UserId { get; set; }
    public string TemplateId { get; set; }
    public string Username { get; set; }
    public string ConnectionType { get; set; } = "SignalR"; // Default to SignalR, can be "WebSocket"
  }

  [HttpPost("generate")]
  public IActionResult GenerateDocument([FromBody] DocumentGenerationRequest request)
  {
    Guid userId = Guid.TryParse(request?.UserId, out Guid id) ? id : Guid.Empty;
    if (string.IsNullOrEmpty(request?.UserId) && userId == Guid.Empty)
    {
      return BadRequest(new { error = "UserId is required" });
    }

    // Get the template ID and username from the request
    string templateId = request?.TemplateId ?? "default";
    string username = request?.Username ?? userId.ToString();
    string connectionType = request?.ConnectionType ?? "SignalR";

    // Immediately return a response
    Task.Run(async () =>
    {
      // Create a new scope for the background task to resolve scoped dependencies
      using var scope = _serviceProvider.CreateScope();
      var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();
      var messagePersistence = scope.ServiceProvider.GetRequiredService<IMessagePersistenceService>();

      // Simulate document generation delay (10 seconds as specified)
      await Task.Delay(10000);

      // Format the notification message
      string message = $"TEMPLATE_ID:{templateId}|Document generated successfully";

      try
      {
        // Send notification based on connection type
        if (connectionType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
          // Send via WebSocket
          await WebSocketHandler.SendMessageAsync(userId.ToString(), message);
        }
        else
        {
          // Send via SignalR with SQL Server persistence (default)
          // Save message to database first for reliability
          var messageId = await messagePersistence.SaveMessageAsync(userId.ToString(), message, "System");

          // Get active connections for the target user
          var connectionIds = await connectionManager.GetConnectionIdsForUserAsync(userId.ToString());
          var connectionIdsList = connectionIds.ToList();

          if (connectionIdsList.Any())
          {
            // User is online - send message immediately
            await _hubContext.Clients.Clients(connectionIdsList).SendAsync("ReceiveNotification",
              username, message);

            // Mark message as delivered for each connection
            foreach (var connectionId in connectionIdsList)
            {
              await messagePersistence.MarkMessageAsDeliveredAsync(messageId, connectionId);
            }

            _logger.LogInformation("Document generation notification sent to {ConnectionCount} connections for user {UserId}",
              connectionIdsList.Count, userId);
          }
          else
          {
            // User is offline - message will be delivered when they reconnect
            _logger.LogInformation("Document generation notification queued for offline user {UserId}", userId);
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error sending document generation notification to user {UserId}", userId);
      }
    });

    return Ok(new {
      status = "Started",
      message = $"Document is generating for user {userId} with template {templateId}...",
      templateId = templateId,
      connectionType = connectionType
    });
  }
}
