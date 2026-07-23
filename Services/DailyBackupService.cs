using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FourierIT_API.Services
{
    public class DailyBackupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyBackupService> _logger;

        public DailyBackupService(
            IServiceProvider serviceProvider,
            ILogger<DailyBackupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Calculate delay until next 00:00 (Midnight)
                var now = DateTime.Now;
                var nextRunTime = now.Date.AddDays(1); // Next midnight
                var delay = nextRunTime - now;

                _logger.LogInformation("Automatic Daily Backup scheduled for {NextRunTime} (in {Hours}h {Minutes}m).",
                    nextRunTime, (int)delay.TotalHours, delay.Minutes);

                // Wait until midnight
                await Task.Delay(delay, stoppingToken);

                // Execute the backup at midnight
                try
                {
                    _logger.LogInformation("Starting scheduled daily database backup...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                        var request = new CreateBackupRequestDto
                        {
                            UserId = null, // System automated backup
                            IsManualBackup = false
                        };

                        var result = await backupService.CreateDatabaseBackupAsync(request);
                        _logger.LogInformation("Scheduled backup status: {StatusMessage}", result.StatusMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during scheduled daily database backup.");
                }
            }
        }
    }
}