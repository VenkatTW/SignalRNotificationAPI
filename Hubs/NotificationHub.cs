using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NotificationHub : Hub
{
  // Dictionary to store user connections (username -> list of connection IDs)
  private static readonly ConcurrentDictionary<string, List<string>> UserConnections = new ConcurrentDictionary<string, List<string>>();

  // Method to get connection IDs for a specific user
  public static IEnumerable<string> GetConnectionIdsForUser(string username)
  {
    if (string.IsNullOrEmpty(username) || !UserConnections.TryGetValue(username, out var connectionIds))
      return new List<string>();

    return connectionIds;
  }

  // Method to register a user with their connection ID
  public async Task RegisterUser(string username)
  {
    if (string.IsNullOrEmpty(username))
      return;

    // Add the connection ID to the user's list of connections
    UserConnections.AddOrUpdate(
      username,
      // If the key doesn't exist, create a new list with the current connection ID
      new List<string> { Context.ConnectionId },
      // If the key exists, add the current connection ID to the existing list
      (key, existingList) =>
      {
        if (!existingList.Contains(Context.ConnectionId))
          existingList.Add(Context.ConnectionId);
        return existingList;
      }
    );

    await Clients.Caller.SendAsync("ReceiveNotification", username, $"Registered as {username}");
  }

  // Method to send notification to a specific user
  public async Task SendNotification(string targetUser, string message)
  {
    if (string.IsNullOrEmpty(targetUser) || !UserConnections.ContainsKey(targetUser))
      return;

    var connectionIds = UserConnections[targetUser];
    await Clients.Clients(connectionIds).SendAsync("ReceiveNotification", targetUser, message);
  }

  // Override OnConnectedAsync to extract username from query parameters
  public override async Task OnConnectedAsync()
  {
    var httpContext = Context.GetHttpContext();
    if (httpContext != null)
    {
      var username = httpContext.Request.Query["username"].ToString();
      if (!string.IsNullOrEmpty(username))
      {
        // Register the user
        await RegisterUser(username);
      }
    }

    await base.OnConnectedAsync();
  }

  // Override OnDisconnectedAsync to remove the connection ID from the user's list
  public override async Task OnDisconnectedAsync(Exception exception)
  {
    // Find and remove the connection ID from all users
    foreach (var username in UserConnections.Keys)
    {
      if (UserConnections.TryGetValue(username, out var connections))
      {
        connections.Remove(Context.ConnectionId);

        // If the user has no more connections, remove the user from the dictionary
        if (connections.Count == 0)
        {
          UserConnections.TryRemove(username, out _);
        }
      }
    }

    await base.OnDisconnectedAsync(exception);
  }
}
