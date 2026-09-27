using System.Threading.Channels;
using FourierIT_API.Interfaces;

namespace FourierIT_API.Services
{
    /// <summary>A notification email waiting to be sent. ActionPath is an app path such as "/my-documents".</summary>
    public record NotificationEmail(
        string ToEmail,
        string Subject,
        string Heading,
        string Message,
        string? ActionPath = null,
        string? ActionLabel = null);

    public interface INotificationEmailQueue
    {
        /// <summary>Queues an email; returns false if it could not be queued (the queue is full).</summary>
        bool Enqueue(NotificationEmail email);
    }

    /// <summary>
    /// Holds notification emails until <see cref="NotificationEmailSender"/> sends them, so the request that
    /// raised a notification never waits on the mail server (or fails because of it).
    /// </summary>
    public class NotificationEmailQueue : INotificationEmailQueue
    {
        private readonly Channel<NotificationEmail> _channel = Channel.CreateBounded<NotificationEmail>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        public ChannelReader<NotificationEmail> Reader => _channel.Reader;

        public bool Enqueue(NotificationEmail email)
        {
            if (string.IsNullOrWhiteSpace(email.ToEmail)) return false;
            return _channel.Writer.TryWrite(email);
        }
    }

    /// <summary>Sends queued notification emails one at a time in the background.</summary>
    public class NotificationEmailSender : BackgroundService
    {
        private readonly NotificationEmailQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationEmailSender> _logger;

        public NotificationEmailSender(
            NotificationEmailQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationEmailSender> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var email in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendNotificationEmailAsync(
                        email.ToEmail, email.Subject, email.Heading, email.Message, email.ActionPath, email.ActionLabel);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed email never affects the in-app notification, which is already saved.
                    _logger.LogWarning(ex, "Failed to send notification email \"{Subject}\"", email.Subject);
                }
            }
        }
    }
}
