using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// The seeded Super Admin account has no roles at all - it is granted access via a
/// "superadmin" claim that bypasses [Authorize] attributes (see SuperAdminRoleHandler /
/// PermissionAuthorizationHandler). That bypass does not reach the hand-rolled
/// IsAdminOrViewerRole/CanManageDocumentAsync ownership checks inside DocumentController,
/// which previously only recognised specific roles - so the Super Admin could pass the
/// route's [Authorize] attribute but still get Forbidden from the action body for any
/// document they didn't personally own.
/// </summary>
public class SuperAdminDocumentAccessTests
{
    [Fact]
    public async Task Preview_WhenSuperAdminDoesNotOwnDocument_StillReturnsFile()
    {
        var fixture = await CreateFixtureAsync(isSuperAdmin: true);

        var result = await fixture.Controller.Preview(fixture.Document.DocumentId);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task Preview_WhenOrdinaryUserDoesNotOwnDocument_ReturnsForbidden()
    {
        var fixture = await CreateFixtureAsync(isSuperAdmin: false);

        var result = await fixture.Controller.Preview(fixture.Document.DocumentId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RevokeDocumentAccess_WhenSuperAdminDoesNotOwnDocument_Succeeds()
    {
        var fixture = await CreateFixtureAsync(isSuperAdmin: true);
        var approval = new DocumentAccessApproval
        {
            ApprovalId = 1,
            DocumentId = fixture.Document.DocumentId,
            EnquiryRequestId = 1,
            ApprovedByUserId = fixture.Document.UserId,
            ApprovedAt = DateTime.UtcNow,
            IsRevoked = false
        };
        fixture.Context.DocumentAccessApprovals.Add(approval);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Controller.RevokeDocumentAccess(fixture.Document.DocumentId, approval.ApprovalId);

        Assert.IsType<NoContentResult>(result);
        Assert.True((await fixture.Context.DocumentAccessApprovals.FindAsync(approval.ApprovalId))!.IsRevoked);
    }

    private static async Task<Fixture> CreateFixtureAsync(bool isSuperAdmin)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SuperAdminDocumentAccessTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);

        var owner = new User { Id = "owner-1", UserName = "owner1@test.local" };
        var currentUser = isSuperAdmin
            ? new User { Id = "superadmin-1", UserName = "superadmin" }
            : new User { Id = "other-user", UserName = "other@test.local" };
        var document = new Document
        {
            DocumentId = 800,
            UserId = owner.Id,
            FileName = "owned.pdf",
            DocumentTypeId = 901,
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            IsCertified = false
        };
        context.Users.AddRange(owner, currentUser);
        context.Documents.Add(document);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 901, TypeName = "Test Document" });
        await context.SaveChangesAsync();

        var userManager = new Mock<UserManager<User>>(
            MockBehavior.Loose,
            new Mock<IUserStore<User>>().Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        userManager.Setup(x => x.GetRolesAsync(currentUser)).ReturnsAsync(new List<string>());

        var repository = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        repository.Setup(x => x.GetDocumentByIdAsync(document.DocumentId)).ReturnsAsync(document);

        var documentService = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        documentService.Setup(x => x.DownloadDocumentAsync(document.DocumentId, document.UserId))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var complianceService = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogService = new Mock<FourierIT_API.Interfaces.IAuditLogService>();

        var controller = new DocumentController(
            documentService.Object,
            repository.Object,
            userManager.Object,
            context,
            complianceService.Object,
            auditLogService.Object,
            NullLogger<DocumentController>.Instance);

        var claims = new List<Claim>();
        if (isSuperAdmin)
            claims.Add(new Claim("superadmin", "true"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };

        return new Fixture(controller, document, context);
    }

    private sealed record Fixture(DocumentController Controller, Document Document, AppDbContext Context);
}
