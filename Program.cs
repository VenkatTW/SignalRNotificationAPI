using System.Net.WebSockets;
using SignalRNotificationAPI.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply CORS before everything else
app.UseCors("AllowAngularApp");

// Swagger (optional)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting(); // Required for SignalR
app.UseAuthorization();

// Configure WebSockets
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
    ReceiveBufferSize = 4 * 1024 // 4KB
});

// WebSocket middleware
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            string userId = context.Request.Query["userId"];
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await WebSocketHandler.OnConnected(webSocket, userId);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});

// Map Hub
app.MapHub<NotificationHub>("/Hubs/NotificationHub"); // Add leading slash

app.MapControllers();

app.Run();
