using System.Security.Claims;
using System.Text.Json;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>Reminders, escalation, extra time, viewing in the browser, flag follow-through and the review queue's "clearly valid" rule.</summary>
public class AccessFollowUpTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"AccessFollowUp_{Guid.NewGuid():N}").Options);

    private static User AddOwner(AppDbContext context, string id = "owner")
    {
        var user = new User { Id = id, UserName = id, Email = $"{id}@test.local" };
        context.Users.Add(user);
        context.Profiles.Add(new Profile { UserId = id, FirstName = "Nadia", LastName = "Owner" });
        return user;
    }

    private static InstitutionEnquiryRequest AddRequest(AppDbContext context, int id, string status, DateTimeOffset requestDate,
        DateTimeOffset? neededBy = null, string? approvedBy = null)
    {
        if (!context.Institutions.Local.Any(i => i.InstitutionId == 5))
            context.Institutions.Add(new Institution { InstitutionId = 5, InstitutionName = "Example Bank", VerifiedDomain = "bank.test" });
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = id, InstitutionId = 5, TargetUserId = "owner", Status = status, PurposeNote = "FICA",
            RequestDate = requestDate, SubmissionDeadline = neededBy, RequesterEmail = "kyc@bank.test", ApprovedByUserId = approvedBy
        };
        context.InstitutionEnquiryRequests.Add(request);
        return request;
    }

    // ── Reminders and escalation ────────────────────────────────────────────

    [Fact]
    public async Task Reminders_WarnTheInstitutionOnce_BeforeItsAccessEnds()
    {
        await using var context = CreateContext();
        AddOwner(context);
        var now = DateTimeOffset.UtcNow;
        AddRequest(context, 1, "Approved", now.AddDays(-2));
        context.AccessTokens.Add(new AccessToken { TokenId = 1, EnquiryRequestId = 1, TokenString = "t1", UserId = "owner", ExpiryTimeStamp = now.AddHours(10) });
        AddRequest(context, 2, "Approved", now.AddDays(-2));
        context.AccessTokens.Add(new AccessToken { TokenId = 2, EnquiryRequestId = 2, TokenString = "t2", UserId = "owner", ExpiryTimeStamp = now.AddDays(5) }); // not yet
        await context.SaveChangesAsync();

        var notifications = new Mock<IInAppNotificationService>();
        var runner = new AccessRequestReminderRunner(context, new SystemSettingsService(context), notifications.Object, null);
        await runner.RunAsync(now);
        await runner.RunAsync(now.AddMinutes(30));

        notifications.Verify(n => n.EmailExternal("kyc@bank.test", "Your access to documents ends soon", It.IsAny<string>(), null, null), Times.Once);
        Assert.NotNull((await context.AccessTokens.FindAsync(1))!.ExpiryReminderSentAt);
        Assert.Null((await context.AccessTokens.FindAsync(2))!.ExpiryReminderSentAt);
    }

    [Fact]
    public async Task Reminders_NudgeTheOwner_AboutARequestWaitingTooLong()
    {
        await using var context = CreateContext();
        AddOwner(context);
        var now = DateTimeOffset.UtcNow;
        AddRequest(context, 1, "Pending", now.AddDays(-3));
        AddRequest(context, 2, "Pending", now.AddHours(-5)); // too recent
        await context.SaveChangesAsync();

        var notifications = new Mock<IInAppNotificationService>();
        await new AccessRequestReminderRunner(context, new SystemSettingsService(context), notifications.Object, null).RunAsync(now);

        notifications.Verify(n => n.NotifyUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Single() == "owner"),
            "A document request is waiting for you", It.IsAny<string>(), "RequestReminder", null, "/documents/requests", true), Times.Once);
        Assert.NotNull((await context.InstitutionEnquiryRequests.FindAsync(1))!.OwnerReminderSentAt);
        Assert.Null((await context.InstitutionEnquiryRequests.FindAsync(2))!.OwnerReminderSentAt);
    }

    [Fact]
    public async Task Reminders_EscalateAnOverdueRequest_ToComplianceWhenTheOwnerHasNoDepartment()
    {
        await using var context = CreateContext();
        AddOwner(context);
        var now = DateTimeOffset.UtcNow;
        AddRequest(context, 1, "Pending", now.AddDays(-1), neededBy: now.AddHours(-1));
        await context.SaveChangesAsync();

        var notifications = new Mock<IInAppNotificationService>();
        notifications.Setup(n => n.GetUserIdsInRoleAsync("Compliance Officer", null)).ReturnsAsync(new List<string> { "co" });
        await new AccessRequestReminderRunner(context, new SystemSettingsService(context), notifications.Object, null).RunAsync(now);

        notifications.Verify(n => n.NotifyUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Single() == "co"),
            "A document request is overdue", It.IsAny<string>(), "RequestEscalated", null, It.IsAny<string>(), true), Times.Once);
        notifications.Verify(n => n.EmailExternal("kyc@bank.test", "Your document request is overdue", It.IsAny<string>(), null, null), Times.Once);
        Assert.NotNull((await context.InstitutionEnquiryRequests.FindAsync(1))!.EscalatedAt);
    }

    // ── More time ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Extension_TheInstitutionAsks_AndTheOwnerApproves_AccessEndsLater()
    {
        await using var context = CreateContext();
        var owner = AddOwner(context);
        var now = DateTimeOffset.UtcNow;
        AddRequest(context, 1, "Approved", now.AddDays(-2), approvedBy: "owner");
        var oldEnd = now.AddHours(20);
        context.AccessTokens.Add(new AccessToken { TokenId = 1, EnquiryRequestId = 1, TokenString = "t1", UserId = "owner", ExpiryTimeStamp = oldEnd, ExpiryReminderSentAt = now });
        context.DocumentAccessApprovals.Add(new DocumentAccessApproval { ApprovalId = 1, EnquiryRequestId = 1, DocumentId = 1, ApprovedByUserId = "owner", ExpiresAt = oldEnd.UtcDateTime });
        context.InstitutionSessionTokens.Add(new InstitutionSessionToken { InstitutionId = 5, TokenString = "session", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        Assert.IsType<BadRequestObjectResult>(await controller.RequestAccessExtension(1, "session", new RequestAccessExtensionDto { Days = 30, Reason = "Audit" }));
        Assert.IsType<OkObjectResult>(await controller.RequestAccessExtension(1, "session", new RequestAccessExtensionDto { Days = 3, Reason = "Audit still running" }));
        Assert.IsType<ConflictObjectResult>(await controller.RequestAccessExtension(1, "session", new RequestAccessExtensionDto { Days = 3, Reason = "Again" }));

        var pending = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.GetPendingExtensions()).Value)).RootElement;
        Assert.Equal(1, pending.GetArrayLength());

        Assert.IsType<OkObjectResult>(await controller.ApproveExtension(1));

        var token = await context.AccessTokens.FindAsync(1);
        Assert.Equal(oldEnd.AddDays(3), token!.ExpiryTimeStamp);
        Assert.Null(token.ExpiryReminderSentAt); // warned again before the new end
        Assert.Equal(oldEnd.AddDays(3).UtcDateTime, (await context.DocumentAccessApprovals.FindAsync(1))!.ExpiresAt);
        Assert.Equal("Approved", (await context.InstitutionEnquiryRequests.FindAsync(1))!.ExtensionStatus);
    }

    [Fact]
    public async Task Extension_OnlyTheOwnerWhoApprovedCanDecide()
    {
        await using var context = CreateContext();
        AddOwner(context);
        var stranger = new User { Id = "someone-else", UserName = "someone-else" };
        context.Users.Add(stranger);
        var request = AddRequest(context, 1, "Approved", DateTimeOffset.UtcNow.AddDays(-2), approvedBy: "owner");
        request.ExtensionStatus = "Pending";
        request.ExtensionRequestedUntil = DateTimeOffset.UtcNow.AddDays(3);
        await context.SaveChangesAsync();

        Assert.IsType<ForbidResult>(await CreateController(context, stranger).ApproveExtension(1));
    }

    // ── Viewing in the browser ──────────────────────────────────────────────

    [Fact]
    public async Task View_ShowsAPdfInline_AndRecordsAView()
    {
        await using var context = CreateContext();
        var owner = AddOwner(context);
        AddRequest(context, 1, "Approved", DateTimeOffset.UtcNow.AddDays(-1));
        context.Documents.Add(new Document { DocumentId = 7, UserId = "owner", DocumentTypeId = 1, FileName = "id.pdf", CurrentStatus = "Approved", ExpiryDate = DateTimeOffset.UtcNow.AddYears(1) });
        context.DocumentAccessApprovals.Add(new DocumentAccessApproval { ApprovalId = 1, EnquiryRequestId = 1, DocumentId = 7, ApprovedByUserId = "owner", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        context.InstitutionSessionTokens.Add(new InstitutionSessionToken { InstitutionId = 5, TokenString = "session", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await context.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(context, owner, audit);

        var result = Assert.IsType<FileContentResult>(await controller.DownloadWithInstitutionToken(7, "session", inline: true));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(string.IsNullOrEmpty(result.FileDownloadName));
        Assert.Equal("inline", controller.Response.Headers["Content-Disposition"].ToString());
        audit.Verify(a => a.CreateAuditLogAsync(It.Is<AuditLog>(l => l.ActionCode == "INSTITUTION_DOCUMENT_VIEWED")), Times.Once);
    }

    // ── Flags ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task InstitutionUpdates_IncludeAResolvedFlag_WithWhatWasDone()
    {
        await using var context = CreateContext();
        var owner = AddOwner(context);
        AddRequest(context, 1, "Approved", DateTimeOffset.UtcNow.AddDays(-1));
        context.Documents.Add(new Document { DocumentId = 7, UserId = "owner", DocumentTypeId = 1, FileName = "id.pdf", CurrentStatus = "Approved", ExpiryDate = DateTimeOffset.UtcNow.AddYears(1) });
        context.EnquiryFlags.Add(new EnquiryFlag { EnquiryFlagId = 1, DocumentId = 7, EnquiryId = 1, FlagReason = "Blurry", IsResolved = true, ResolvedAt = DateTimeOffset.UtcNow, ResolutionNote = "Uploaded a clearer copy." });
        context.InstitutionSessionTokens.Add(new InstitutionSessionToken { InstitutionId = 5, TokenString = "session", IssuedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await context.SaveChangesAsync();

        var feed = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(await CreateController(context, owner).GetInstitutionNotifications("session")).Value)).RootElement;

        var item = feed.EnumerateArray().Single(i => i.GetProperty("Status").GetString() == "Flag resolved");
        Assert.Contains("Uploaded a clearer copy.", item.GetProperty("Message").GetString());
    }

    // ── Review queue ────────────────────────────────────────────────────────

    [Fact]
    public void ClearlyValid_OnlyWhenTheChecksFoundNothingWrong()
    {
        Assert.Empty(ComplianceController.ClearlyValidConcerns(new DocumentComplianceCheck { DaysUntilExpiry = 200 }));
        Assert.Contains("Not certified", ComplianceController.ClearlyValidConcerns(new DocumentComplianceCheck { IsCertified = false }));
        Assert.Contains("Expired or expiry date not valid", ComplianceController.ClearlyValidConcerns(new DocumentComplianceCheck { DaysUntilExpiry = 0 }));
        Assert.Contains("Missing page", ComplianceController.ClearlyValidConcerns(new DocumentComplianceCheck { NonComplianceReason = "Missing page" }));
    }

    private static DocumentAccessRequestsController CreateController(AppDbContext context, User signedIn, Mock<IAuditLogService>? audit = null)
    {
        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(), new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(signedIn);

        var documents = new Mock<IDocumentService>();
        documents.Setup(d => d.DownloadDocumentAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(new byte[] { 1, 2, 3 });

        return new DocumentAccessRequestsController(context, userManager.Object, documents.Object,
            new DepartmentRequestValidationService(), (audit ?? new Mock<IAuditLogService>()).Object, new Mock<IComplianceService>().Object,
            notifications: new Mock<IInAppNotificationService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
