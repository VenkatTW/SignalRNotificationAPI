using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SignalRNotificationAPI.WebSockets;

[ApiController]
[Route("api/[controller]")]
public class DocumentGeneratorController : ControllerBase
{
  private readonly IHubContext<NotificationHub> _hubContext;

  public DocumentGeneratorController(IHubContext<NotificationHub> hubContext)
  {
    _hubContext = hubContext;
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
      // Simulate document generation delay (10 seconds as specified)
      await Task.Delay(10000);

      // Format the notification message
      string message = $"TEMPLATE_ID:{templateId}|Document generated successfully";

      // Send notification based on connection type
      if (connectionType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
      {
        // Send via WebSocket
        await WebSocketHandler.SendMessageAsync(userId.ToString(), message);
      }
      else
      {
        // Send via SignalR (default)
        var connectionIds = NotificationHub.GetConnectionIdsForUser(userId.ToString());
        if (connectionIds.Any())
        {
          await _hubContext.Clients.Clients(connectionIds).SendAsync("ReceiveNotification",
            username, message);
        }
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
