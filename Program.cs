using System.Net.WebSockets;
using SignalRNotificationAPI.WebSockets;
using SignalRNotificationAPI.Data;
using SignalRNotificationAPI.Services;
using SignalRNotificationAPI.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

// Configure Entity Framework
builder.Services.AddDbContext<SignalRDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Register custom services - Use a single instance for both interfaces
builder.Services.AddScoped<SqlServerConnectionManager>();
builder.Services.AddScoped<IConnectionManager>(provider => provider.GetRequiredService<SqlServerConnectionManager>());
builder.Services.AddScoped<IMessagePersistenceService>(provider => provider.GetRequiredService<SqlServerConnectionManager>());

// Register background services
builder.Services.AddHostedService<SignalRCleanupService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        var angularAppUrl = builder.Configuration["CorsSettings:AngularAppUrl"];
        policy.WithOrigins(angularAppUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply database migrations on startup (for POC deployment)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SignalRDbContext>();
    context.Database.Migrate();
}

// Apply CORS before everything else
app.UseCors("AllowAngularApp");

// Map health check endpoint
app.MapHealthChecks("/health");

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
