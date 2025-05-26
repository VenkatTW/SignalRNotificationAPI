using Microsoft.Extensions.Options;

namespace SignalRNotificationAPI.Services
{
    public class SignalRCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SignalRCleanupService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _cleanupInterval;

        public SignalRCleanupService(
            IServiceProvider serviceProvider,
            ILogger<SignalRCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;

            // Get cleanup interval from configuration, default to 15 minutes
            var intervalMinutes = _configuration.GetValue<int>("SignalRSettings:CleanupIntervalMinutes", 15);
            _cleanupInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR Cleanup Service started with interval: {Interval}", _cleanupInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during cleanup operation");
                    // Wait a shorter time before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("SignalR Cleanup Service stopped");
        }

        private async Task PerformCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();
            var messagePersistence = scope.ServiceProvider.GetRequiredService<IMessagePersistenceService>();

            try
            {
                _logger.LogDebug("Starting cleanup operations");

                // Cleanup stale connections
                await connectionManager.CleanupStaleConnectionsAsync();

                // Cleanup expired messages
                await messagePersistence.CleanupExpiredMessagesAsync();

                // Log current statistics
                var activeConnections = await connectionManager.GetActiveConnectionCountAsync();
                _logger.LogInformation("Cleanup completed. Active connections: {ActiveConnections}", activeConnections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup operations");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR Cleanup Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}
