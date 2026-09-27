using System.Text.Json;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>The live reports: certificates must never overstate compliance, and only real data appears.</summary>
public class ReportInsightsTests
{
    private static AppDbContext CreateContext()
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportInsightsTests_{Guid.NewGuid():N}")
            .Options);

        context.Roles.AddRange(
            new Role { Id = "DO", Name = "Document Owner", NormalizedName = "DOCUMENT OWNER" },
            new Role { Id = "SH", Name = "Stakeholder", NormalizedName = "STAKEHOLDER" });
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" });
        return context;
    }

    private static User AddUser(AppDbContext context, string id, string roleId, string first)
    {
        var user = new User { Id = id, UserName = id, Email = $"{id}@test.local" };
        context.Users.Add(user);
        context.Profiles.Add(new Profile { UserId = id, FirstName = first, LastName = "Test" });
        context.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId });
        return user;
    }

    private static JsonElement Json(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value)).RootElement;

    [Fact]
    public async Task Certificate_ForANonCompliantOwner_IsNotACertificateAndListsWhatIsMissing()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        context.ComplianceStatuses.Add(new ComplianceStatus
        {
            UserId = "owner", OverallStatus = "Non-Compliant", CompliancePercentage = 40, MissingDocuments = 2, RiskLevel = "Medium"
        });
        await context.SaveChangesAsync();

        var json = Json(await new ReportInsightsController(context).GetComplianceCertificate("owner"));

        Assert.False(json.GetProperty("isCompliant").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("validUntil").ValueKind);
        Assert.Contains("2 required documents are missing.", json.GetProperty("issues").EnumerateArray().Select(i => i.GetString()));
    }

    [Fact]
    public async Task Certificate_ForACompliantOwner_IsValidUntilTheEarliestExpiry()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        context.ComplianceStatuses.Add(new ComplianceStatus { UserId = "owner", OverallStatus = "Compliant", CompliancePercentage = 100, RiskLevel = "Low" });
        var soonest = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
        context.Documents.AddRange(
            new Document { DocumentId = 1, UserId = "owner", DocumentTypeId = 1, FileName = "id.pdf", CurrentStatus = "Verified", ExpiryDate = soonest },
            new Document { DocumentId = 2, UserId = "owner", DocumentTypeId = 1, FileName = "id2.pdf", CurrentStatus = "Verified", ExpiryDate = soonest.AddYears(1) });
        await context.SaveChangesAsync();

        var json = Json(await new ReportInsightsController(context).GetComplianceCertificate("owner"));

        Assert.True(json.GetProperty("isCompliant").GetBoolean());
        Assert.Equal(soonest, json.GetProperty("validUntil").GetDateTimeOffset());
    }

    [Fact]
    public async Task Certificate_IsOnlyForDocumentOwners()
    {
        await using var context = CreateContext();
        AddUser(context, "viewer", "SH", "Sam");
        await context.SaveChangesAsync();

        Assert.IsType<NotFoundObjectResult>(await new ReportInsightsController(context).GetComplianceCertificate("viewer"));
    }

    [Fact]
    public async Task RiskRating_GroupsOwnersByLevelWithTheirFactors_AndLeavesOutNonUploaders()
    {
        await using var context = CreateContext();
        AddUser(context, "risky", "DO", "Risky");
        AddUser(context, "fine", "DO", "Fine");
        AddUser(context, "new", "DO", "New");
        AddUser(context, "viewer", "SH", "Viewer");
        context.ComplianceStatuses.AddRange(
            new ComplianceStatus { UserId = "risky", RiskLevel = "High", OverallRiskScore = 55, ExpiredDocuments = 1, MissingDocuments = 1 },
            new ComplianceStatus { UserId = "fine", RiskLevel = "Low", OverallRiskScore = 5 });
        await context.SaveChangesAsync();

        var json = Json(await new ReportInsightsController(context).GetClientRiskRating());

        Assert.Equal(3, json.GetProperty("totalProfiles").GetInt32());
        var strata = json.GetProperty("strata").EnumerateArray().ToDictionary(s => s.GetProperty("level").GetString()!, s => s.GetProperty("profiles"));
        Assert.Equal(new[] { "High", "Low", "Not assessed" }, strata.Keys);
        var high = strata["High"].EnumerateArray().Single();
        Assert.Equal(new[] { "1 expired document", "1 missing document" }, high.GetProperty("factors").EnumerateArray().Select(f => f.GetString()));
    }

    [Fact]
    public async Task SystemAudit_PutsEachEventUnderItsInstitution()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        context.Institutions.Add(new Institution { InstitutionId = 5, InstitutionName = "Example Bank", VerifiedDomain = "bank.test" });
        context.InstitutionEnquiryRequests.Add(new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 9, InstitutionId = 5, TargetUserId = "owner", Status = "Approved", PurposeNote = "FICA", RequestDate = DateTimeOffset.UtcNow
        });
        context.DocumentAccessApprovals.Add(new DocumentAccessApproval { ApprovalId = 3, EnquiryRequestId = 9, DocumentId = 1, ApprovedByUserId = "owner" });
        context.AuditLogs.AddRange(
            // The owner's approval names only the request; the download names the approval. Both belong to Example Bank.
            new AuditLog { ActionCode = "REQUEST_APPROVED", UserId = "owner", TableAffected = "InstitutionEnquiryRequests", RecordID = 9, Description = "Approved", TimeStamp = DateTimeOffset.UtcNow },
            new AuditLog { ActionCode = "INSTITUTION_DOCUMENT_DOWNLOADED", InstitutionId = 5, TableAffected = "DocumentAccessApprovals", RecordID = 3, Description = "Downloaded", TimeStamp = DateTimeOffset.UtcNow },
            new AuditLog { ActionCode = "LOGIN_SUCCESS", UserId = "owner", TableAffected = "Users", Description = "Not institution activity", TimeStamp = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        var json = Json(await new ReportInsightsController(context).GetSystemAudit());

        var institution = json.GetProperty("institutions").EnumerateArray().Single();
        Assert.Equal("Example Bank", institution.GetProperty("institutionName").GetString());
        Assert.Equal(2, institution.GetProperty("events").GetArrayLength());
        Assert.Equal(1, institution.GetProperty("downloads").GetInt32());
        Assert.Equal(1, json.GetProperty("totalDownloads").GetInt32());
    }

    [Fact]
    public async Task ActivityReport_ShowsRealStatusesInstitutionDownloadsAndSharing()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        var now = DateTimeOffset.UtcNow;
        context.Documents.AddRange(
            new Document { DocumentId = 1, UserId = "owner", DocumentTypeId = 1, FileName = "id.pdf", CurrentStatus = "Approved", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(2) },
            new Document { DocumentId = 2, UserId = "owner", DocumentTypeId = 1, FileName = "bad.pdf", CurrentStatus = "Rejected", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(2) });
        context.Institutions.Add(new Institution { InstitutionId = 5, InstitutionName = "Example Bank", VerifiedDomain = "bank.test" });
        context.InstitutionEnquiryRequests.Add(new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 9, InstitutionId = 5, TargetUserId = "owner", Status = "Approved", PurposeNote = "FICA", RequestDate = now
        });
        // Approved with no end date: still active.
        context.DocumentAccessApprovals.Add(new DocumentAccessApproval { ApprovalId = 3, EnquiryRequestId = 9, DocumentId = 1, ApprovedByUserId = "owner" });
        context.AuditLogs.Add(new AuditLog
        {
            ActionCode = "INSTITUTION_DOCUMENT_DOWNLOADED", InstitutionId = 5, TableAffected = "DocumentAccessApprovals", RecordID = 3,
            Description = "Downloaded \"id.pdf\" under request #9.", TimeStamp = now
        });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).GetActivityReport("owner");
        var report = Assert.IsType<FourierIT_API.DTOs.Reports.ActivityReportDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("Rejected", report.Inventory.Single(d => d.DocumentName == "bad.pdf").VerificationStatus);
        Assert.Equal(1, report.ActiveDocuments);
        Assert.Equal(1, report.InactiveDocuments);
        var download = Assert.Single(report.VaultAccessLog);
        Assert.Equal("Example Bank", download.AccessorName);
        Assert.Equal("Institution", download.AccessorRole);
        var client = Assert.Single(report.ClientRelationships);
        Assert.Equal(("Example Bank", 1, "Active"), (client.Organisation, client.DocumentsShared, client.Status));
        Assert.Null(report.ComplianceStatus);
    }

    [Fact]
    public async Task AccessHistory_ListsOnlyGrantedAccess_WithWhatWasActuallyDownloaded()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        context.Institutions.Add(new Institution { InstitutionId = 5, InstitutionName = "Example Bank", VerifiedDomain = "bank.test" });
        var now = DateTimeOffset.UtcNow;
        context.InstitutionEnquiryRequests.AddRange(
            new InstitutionEnquiryRequest { EnquiryRequestId = 9, InstitutionId = 5, TargetUserId = "owner", Status = "Approved", PurposeNote = "FICA", RequestDate = now, RespondedAt = DateTime.UtcNow },
            new InstitutionEnquiryRequest { EnquiryRequestId = 10, InstitutionId = 5, TargetUserId = "owner", Status = "Pending", PurposeNote = "FICA", RequestDate = now },
            new InstitutionEnquiryRequest { EnquiryRequestId = 11, InstitutionId = 5, TargetUserId = "owner", Status = "Revoked", PurposeNote = "FICA", RequestDate = now, RespondedAt = DateTime.UtcNow });
        context.AuditLogs.Add(new AuditLog
        {
            ActionCode = "INSTITUTION_DOCUMENT_DOWNLOADED", InstitutionId = 5, TableAffected = "InstitutionEnquiryRequests", RecordID = 9,
            Description = "Downloaded \"id.pdf\" under request #9.", TimeStamp = now
        });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).GetInstitutionAccessHistoryReport(null, null, null, null, null, null);
        var rows = Assert.IsAssignableFrom<IEnumerable<FourierIT_API.DTOs.Reports.InstitutionAccessHistoryReportRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value)
            .ToDictionary(r => r.RequestId);

        Assert.Equal(new[] { 9, 11 }, rows.Keys.OrderBy(id => id));
        Assert.Equal("Active", rows[9].AccessStatus);
        Assert.Equal(new[] { "id.pdf" }, rows[9].DocumentsAccessed);
        Assert.Equal("Revoked", rows[11].AccessStatus);
        Assert.Empty(rows[11].DocumentsAccessed);
    }

    [Fact]
    public async Task OwnerCompliance_DoesNotCountARejectedDocumentAsUploaded()
    {
        await using var context = CreateContext();
        var owner = AddUser(context, "owner", "DO", "Nadia");
        owner.EntityTypeId = 1;
        context.EntityTypes.Add(new EntityType { EntityTypeId = 1, Name = "Individual" });
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 2, TypeName = "Proof of Address" });
        context.RequiredDocuments.AddRange(
            new RequiredDocument { RequiredDocumentId = 1, EntityTypeId = 1, DocumentTypeId = 1 },
            new RequiredDocument { RequiredDocumentId = 2, EntityTypeId = 1, DocumentTypeId = 2 });
        var now = DateTimeOffset.UtcNow;
        context.Documents.AddRange(
            new Document { DocumentId = 1, UserId = "owner", DocumentTypeId = 1, FileName = "id.pdf", CurrentStatus = "Approved", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(1) },
            new Document { DocumentId = 2, UserId = "owner", DocumentTypeId = 2, FileName = "address.pdf", CurrentStatus = "Rejected", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(1) });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).GetDocumentOwnerComplianceReport(null, null, null, null, null, null, null);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<FourierIT_API.DTOs.Reports.DocumentOwnerComplianceReportRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value));

        Assert.Equal(1, row.UploadedDocuments);
        Assert.Equal(1, row.MissingDocuments);
        Assert.Equal(new[] { "Proof of Address" }, row.MissingDocumentNames);
    }

    [Fact]
    public async Task MonthlyReport_CountsRejectedDocuments_AndOnlyRealSecurityEvents()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        var now = DateTimeOffset.UtcNow;
        context.Documents.AddRange(
            new Document { DocumentId = 1, UserId = "owner", DocumentTypeId = 1, FileName = "ok.pdf", CurrentStatus = "Approved", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(1) },
            new Document { DocumentId = 2, UserId = "owner", DocumentTypeId = 1, FileName = "bad.pdf", CurrentStatus = "Rejected", UploadedDate = DateTime.UtcNow, ExpiryDate = now.AddYears(1) });
        context.AuditLogs.AddRange(
            new AuditLog { ActionCode = "LOGIN_SUCCESS", UserId = "owner", TableAffected = "Users", TimeStamp = now },
            new AuditLog { ActionCode = "LOGIN_FAILURE", UserId = "owner", TableAffected = "Users", TimeStamp = now },
            new AuditLog { ActionCode = "DOCUMENT_FLAG_RESOLVED", UserId = "owner", TableAffected = "Documents", TimeStamp = now });
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.AddHours(2).Date;
        var result = await new ReportsController(context).GetMonthlyReport(today, today);
        var report = Assert.IsType<FourierIT_API.DTOs.Reports.MonthlyReportDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(1, report.Processing.Rejected);
        Assert.Equal(0, report.Processing.FlaggedAnomalies);
        Assert.Equal(1, report.SecurityEvents.Sum(e => e.FailedLogins));
        Assert.Equal(0, report.SecurityEvents.Sum(e => e.UnusualAccessPattern)); // a normal login is not a security event
    }

    [Fact]
    public async Task NearExpiryReport_ListsOnlyDocumentsInsideTheirWarningPeriod()
    {
        await using var context = CreateContext();
        AddUser(context, "owner", "DO", "Nadia");
        context.DocumentTypes.AddRange(
            new DocumentType { DocumentTypeId = 2, TypeName = "Bank Statement", WarningDays = 30 },
            new DocumentType { DocumentTypeId = 3, TypeName = "South African ID Book", ValidityMonths = 120, WarningDays = 30 });
        var now = DateTimeOffset.UtcNow;
        context.Documents.AddRange(
            new Document { DocumentId = 1, UserId = "owner", DocumentTypeId = 2, FileName = "soon.pdf", CurrentStatus = "Verified", ExpiryDate = now.AddDays(10) },
            new Document { DocumentId = 2, UserId = "owner", DocumentTypeId = 2, FileName = "later.pdf", CurrentStatus = "Verified", ExpiryDate = now.AddDays(200) },
            new Document { DocumentId = 3, UserId = "owner", DocumentTypeId = 3, FileName = "id.pdf", CurrentStatus = "Verified", ExpiryDate = now.AddYears(9) });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).GetExpiringDocumentsReport(null, null, null, null, null, null);

        var rows = Assert.IsAssignableFrom<IEnumerable<FourierIT_API.DTOs.Reports.ExpiringDocumentsReportRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        var row = Assert.Single(rows);
        Assert.Equal("Bank Statement", row.DocumentType);
        Assert.Equal("No department", row.Department);
        Assert.InRange(row.DaysRemaining, 9, 10);
    }
}
