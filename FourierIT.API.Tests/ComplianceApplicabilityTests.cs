using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FourierIT.API.Tests;

public class ComplianceApplicabilityTests
{
    [Theory]
    [InlineData("Stakeholder")]
    [InlineData("Compliance Officer")]
    public async Task CheckUserCompliance_ForRoleThatDoesNotUploadDocuments_IsNotApplicableAndSavesNothing(string role)
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ComplianceApplicabilityTests_{Guid.NewGuid():N}")
            .Options);
        var user = new User { Id = "user-1", UserName = "viewer", Email = "viewer@test.local" };

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { role });

        var service = new ComplianceService(
            context,
            userManager.Object,
            NullLogger<ComplianceService>.Instance,
            new Mock<IAuditLogService>().Object);

        var status = await service.CheckUserComplianceAsync(user.Id);

        Assert.Equal(ComplianceService.NotApplicableStatus, status.OverallStatus);
        Assert.False(await context.ComplianceStatuses.AnyAsync());
        Assert.False(await context.ComplianceAlerts.AnyAsync());
    }

    [Fact]
    public async Task GetPendingManualReviews_IncludesRegularPendingReviewChecks()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ComplianceApplicabilityTests_{Guid.NewGuid():N}")
            .Options);

        var owner = new User { Id = "owner-1", UserName = "owner1@test.local", Email = "owner1@test.local" };
        var documentType = new DocumentType { DocumentTypeId = 101, TypeName = "Identity Document" };
        var document = new Document
        {
            DocumentId = 202,
            UserId = owner.Id,
            User = owner,
            DocumentTypeId = documentType.DocumentTypeId,
            DocumentType = documentType,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            UploadedDate = DateTime.UtcNow,
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 1024,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var complianceStatus = new ComplianceStatus
        {
            ComplianceStatusId = 303,
            UserId = owner.Id,
            User = owner,
            OverallStatus = "Pending",
            CompliancePercentage = 0,
            PendingReviewDocuments = 1
        };
        context.Users.Add(owner);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(document);
        context.ComplianceStatuses.Add(complianceStatus);
        context.DocumentComplianceChecks.Add(new DocumentComplianceCheck
        {
            CheckId = 404,
            DocumentId = document.DocumentId,
            Document = document,
            ComplianceStatusId = complianceStatus.ComplianceStatusId,
            ComplianceStatus = complianceStatus,
            CheckStatus = "Pending Review",
            RequiresManualReview = false,
            IsManuallyApproved = false,
            CheckedAt = DateTime.UtcNow,
            DocumentTypeId = documentType.DocumentTypeId
        });
        await context.SaveChangesAsync();

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var service = new ComplianceService(
            context,
            userManager.Object,
            NullLogger<ComplianceService>.Instance,
            new Mock<IAuditLogService>().Object);

        var pending = await service.GetPendingManualReviewsAsync();

        Assert.Single(pending);
        Assert.Equal("Pending Review", pending[0].CheckStatus);
    }
}
