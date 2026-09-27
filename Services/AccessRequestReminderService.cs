using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Services
{
    /// <summary>
    /// Follows up on institution document requests so nothing stalls unnoticed:
    /// warns institutions before their access ends, reminds owners about requests they haven't answered,
    /// and escalates requests whose needed-by date has passed.
    /// </summary>
    public class AccessRequestReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AccessRequestReminderService> _logger;

        public AccessRequestReminderService(IServiceProvider serviceProvider, ILogger<AccessRequestReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the API finish starting (and apply migrations) before the first pass.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var runner = new AccessRequestReminderRunner(
                        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                        scope.ServiceProvider.GetRequiredService<ISystemSettingsService>(),
                        scope.ServiceProvider.GetRequiredService<IInAppNotificationService>(),
                        scope.ServiceProvider.GetService<IAuditLogService>());
                    await runner.RunAsync(DateTimeOffset.UtcNow);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Access request reminders failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }

    /// <summary>One pass of the reminders. Each reminder is sent once (its time is recorded), so running it again is safe.</summary>
    public class AccessRequestReminderRunner
    {
        private static readonly string[] AwaitingAnswer = { "Pending", "Department_Pending" };

        private readonly AppDbContext _context;
        private readonly ISystemSettingsService _settings;
        private readonly IInAppNotificationService _notifications;
        private readonly IAuditLogService? _audit;

        public AccessRequestReminderRunner(AppDbContext context, ISystemSettingsService settings,
            IInAppNotificationService notifications, IAuditLogService? audit)
        {
            _context = context;
            _settings = settings;
            _notifications = notifications;
            _audit = audit;
        }

        public async Task RunAsync(DateTimeOffset now)
        {
            await WarnInstitutionsBeforeAccessEndsAsync(now);
            await RemindOwnersAsync(now);
            await EscalateOverdueRequestsAsync(now);
        }

        /// <summary>Addition 1: email the institution once, shortly before its access to the documents ends.</summary>
        private async Task WarnInstitutionsBeforeAccessEndsAsync(DateTimeOffset now)
        {
            var warnWithin = TimeSpan.FromHours(await _settings.GetAsync(SystemSettingDefinitions.AccessExpiryReminderHours));
            var cutoff = now + warnWithin;

            var tokens = await _context.AccessTokens
                .Include(token => token.institutionEnquiryRequest)
                .Where(token => !token.IsRevoked
                    && token.ExpiryReminderSentAt == null
                    && token.ExpiryTimeStamp > now
                    && token.ExpiryTimeStamp <= cutoff
                    && token.institutionEnquiryRequest.Status == "Approved")
                .ToListAsync();

            foreach (var token in tokens)
            {
                var request = token.institutionEnquiryRequest;
                var endsAt = token.ExpiryTimeStamp.ToOffset(TimeSpan.FromHours(2));
                _notifications.EmailExternal(request.RequesterEmail, "Your access to documents ends soon",
                    $"Your access to the documents for {Describe(request)} ends on {endsAt:d MMM yyyy 'at' HH:mm} (SAST). " +
                    "Download what you need before then. If you need the documents for longer, sign in to the DocuVault institution portal and ask for more time on the request.");
                token.ExpiryReminderSentAt = now;
                await LogAsync("ACCESS_EXPIRY_REMINDER_SENT", request, $"Reminded the institution that access for {Describe(request)} ends {endsAt:yyyy-MM-dd HH:mm}.");
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>Addition 4: remind whoever has to answer a request that has been waiting too long.</summary>
        private async Task RemindOwnersAsync(DateTimeOffset now)
        {
            var remindAfter = TimeSpan.FromHours(await _settings.GetAsync(SystemSettingDefinitions.OwnerReminderAfterHours));
            var waitingSince = now - remindAfter;

            var requests = await _context.InstitutionEnquiryRequests
                .Include(request => request.Institution)
                .Where(request => AwaitingAnswer.Contains(request.Status)
                    && request.OwnerReminderSentAt == null
                    && request.RequestDate <= waitingSince)
                .ToListAsync();

            foreach (var request in requests)
            {
                var (recipients, link) = await ResponsiblePeopleAsync(request);
                if (recipients.Count == 0) continue;

                var due = request.SubmissionDeadline is { } deadline
                    ? $" It is needed by {deadline.ToOffset(TimeSpan.FromHours(2)):d MMM yyyy}."
                    : string.Empty;
                await _notifications.NotifyUsersAsync(recipients, "A document request is waiting for you",
                    $"{request.Institution.InstitutionName}'s {Describe(request)} has been waiting since {request.RequestDate.ToOffset(TimeSpan.FromHours(2)):d MMM yyyy}.{due} Please approve or deny it.",
                    "RequestReminder", link: link, sendEmail: true);

                request.OwnerReminderSentAt = now.UtcDateTime;
                await LogAsync("REQUEST_REMINDER_SENT", request, $"Reminded the owner about {Describe(request)}.");
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Addition 4: once the needed-by date passes without an answer, tell the department admin
        /// (or the Compliance Officers, when there is no department) and let the institution know.
        /// </summary>
        private async Task EscalateOverdueRequestsAsync(DateTimeOffset now)
        {
            var requests = await _context.InstitutionEnquiryRequests
                .Include(request => request.Institution)
                .Include(request => request.TargetUser)
                    .ThenInclude(user => user!.Profile)
                .Where(request => AwaitingAnswer.Contains(request.Status)
                    && request.EscalatedAt == null
                    && request.SubmissionDeadline != null
                    && request.SubmissionDeadline < now)
                .ToListAsync();

            foreach (var request in requests)
            {
                var departmentId = request.TargetDepartmentId ?? request.TargetUser?.DepartmentId;
                // A request still with the department is the admins' to answer, so it goes up to Compliance.
                var escalateTo = departmentId != null && request.Status == "Pending"
                    ? await _notifications.GetUserIdsInRoleAsync("Department Admin", departmentId)
                    : new List<string>();
                var escalatedToDepartment = escalateTo.Count > 0;
                if (!escalatedToDepartment)
                    escalateTo = await _notifications.GetUserIdsInRoleAsync("Compliance Officer");

                var who = request.Status == "Department_Pending"
                    ? "the department"
                    : OwnerName(request.TargetUser) ?? "the document owner";
                var deadline = request.SubmissionDeadline!.Value.ToOffset(TimeSpan.FromHours(2));

                await _notifications.NotifyUsersAsync(escalateTo, "A document request is overdue",
                    $"{request.Institution.InstitutionName}'s {Describe(request)} was needed by {deadline:d MMM yyyy} and {who} hasn't answered it yet. Please follow up.",
                    "RequestEscalated", link: escalatedToDepartment ? "/dashboard/department" : "/compliance/review-queue", sendEmail: true);

                _notifications.EmailExternal(request.RequesterEmail, "Your document request is overdue",
                    $"Your {Describe(request)} was needed by {deadline:d MMM yyyy} and hasn't been answered yet. We have asked {(escalatedToDepartment ? "the department admin" : "a Compliance Officer")} to follow it up.");

                request.EscalatedAt = now.UtcDateTime;
                await LogAsync("REQUEST_ESCALATED", request,
                    $"{Describe(request)} passed its needed-by date without an answer and was escalated to {(escalatedToDepartment ? "the department admin" : "the Compliance Officers")}.");
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>Who must answer the request now: the owner, or the department admins while the department still has it.</summary>
        private async Task<(List<string> Recipients, string Link)> ResponsiblePeopleAsync(InstitutionEnquiryRequest request)
        {
            if (request.Status == "Department_Pending" && request.TargetDepartmentId != null)
                return (await _notifications.GetUserIdsInRoleAsync("Department Admin", request.TargetDepartmentId), "/dashboard/department");

            return (string.IsNullOrWhiteSpace(request.TargetUserId) ? new List<string>() : new List<string> { request.TargetUserId },
                "/documents/requests");
        }

        private static string Describe(InstitutionEnquiryRequest request) =>
            string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"request #{request.EnquiryRequestId}"
                : $"request {request.ReferenceNumber} (#{request.EnquiryRequestId})";

        private static string? OwnerName(User? user)
        {
            if (user == null) return null;
            var name = $"{user.Profile?.FirstName} {user.Profile?.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? user.UserName : name;
        }

        private async Task LogAsync(string code, InstitutionEnquiryRequest request, string description)
        {
            if (_audit == null) return;
            try
            {
                await _audit.CreateAuditLogAsync(new AuditLog
                {
                    InstitutionId = request.InstitutionId,
                    ActionCode = code,
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = description,
                    TableAffected = "InstitutionEnquiryRequests",
                    RecordID = request.EnquiryRequestId
                });
            }
            catch
            {
                // The reminder itself matters more than its audit entry.
            }
        }
    }
}
