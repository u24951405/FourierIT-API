using FourierIT_API.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FourierIT_API.Services
{
    public class ExpiredDocumentComplianceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExpiredDocumentComplianceService> _logger;

        public ExpiredDocumentComplianceService(
            IServiceProvider serviceProvider,
            ILogger<ExpiredDocumentComplianceService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expired document compliance service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredDocumentsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed while processing expired document compliance.");
                }

                // Run again in one hour to catch recently expired documents.
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Expired document compliance service is stopping.");
        }

        private async Task ProcessExpiredDocumentsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var complianceService = scope.ServiceProvider.GetRequiredService<IComplianceService>();

            _logger.LogInformation("Running expired document compliance processing.");
            await complianceService.ProcessExpiredDocumentComplianceAsync();
            _logger.LogInformation("Expired document compliance processing completed.");
        }
    }
}
