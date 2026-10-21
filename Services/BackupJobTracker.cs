using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;

namespace FourierIT_API.Services
{
    /// <summary>Where a manual backup has got to: Running, Succeeded or Failed (with the result once finished).</summary>
    public sealed class BackupJobState
    {
        public Guid JobId { get; init; } = Guid.NewGuid();
        public string Status { get; set; } = "Running";
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedAt { get; set; }
        public BackupResponseDto? Result { get; set; }
    }

    /// <summary>
    /// Runs a manual backup in the background so the page doesn't wait on it (the upload can take a while),
    /// and only one at a time. The page asks for the status until the backup finishes.
    /// </summary>
    public sealed class BackupJobTracker
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<BackupJobTracker> _logger;
        private readonly object _gate = new();

        public BackupJobTracker(IServiceScopeFactory scopes, ILogger<BackupJobTracker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        /// <summary>The backup running now, or the last one to finish since the API started.</summary>
        public BackupJobState? Current { get; private set; }

        /// <summary>Starts a backup unless one is already running (then returns false and the running one).</summary>
        public bool TryStart(CreateBackupRequestDto request, out BackupJobState job)
        {
            lock (_gate)
            {
                if (Current is { Status: "Running" } running)
                {
                    job = running;
                    return false;
                }

                job = new BackupJobState();
                Current = job;
            }

            var started = job;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IBackupService>().CreateDatabaseBackupAsync(request);
                    started.Result = result;
                    started.Status = result.Success ? "Succeeded" : "Failed";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The background backup failed.");
                    started.Result = new BackupResponseDto { StatusMessage = "The backup failed: " + ex.Message };
                    started.Status = "Failed";
                }
                finally
                {
                    started.FinishedAt = DateTimeOffset.UtcNow;
                }
            });

            return true;
        }
    }
}
