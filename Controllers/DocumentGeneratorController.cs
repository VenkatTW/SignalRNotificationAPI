using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Threading.Tasks;

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

    // Immediately return a response
    Task.Run(async () =>
    {
      // Simulate document generation delay (10 seconds as specified)
      await Task.Delay(10000);

      // Get the connection IDs for the target user
      var connectionIds = NotificationHub.GetConnectionIdsForUser(userId.ToString());

      // Send the notification directly to the user's connections
      if (connectionIds.Any())
      {
        // Send a structured notification with template ID clearly identified
        await _hubContext.Clients.Clients(connectionIds).SendAsync("ReceiveNotification",
          username, $"TEMPLATE_ID:{templateId}|Document generated successfully");
      }
    });

    return Ok(new {
      status = "Started",
      message = $"Document is generating for user {userId} with template {templateId}...",
      templateId = templateId
    });
  }
}
