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
    public string Username { get; set; }
  }

  [HttpPost("generate")]
  public IActionResult GenerateDocument([FromBody] DocumentGenerationRequest request)
  {
    if (string.IsNullOrEmpty(request?.Username))
    {
      return BadRequest(new { error = "Username is required" });
    }

    string username = request.Username;

    // Immediately return a response
    Task.Run(async () =>
    {
      // Simulate document generation delay (10 seconds as specified)
      await Task.Delay(10000);

      // Get the connection IDs for the target user
      var connectionIds = NotificationHub.GetConnectionIdsForUser(username);

      // Send the notification directly to the user's connections
      if (connectionIds.Any())
      {
        await _hubContext.Clients.Clients(connectionIds).SendAsync("ReceiveNotification",
          username, $"Hello {username}, your document has been generated!");
      }
    });

    return Ok(new { status = "Started", message = $"Document is generating for user {username}..." });
  }
}
