using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FourierIT.API.Tests;

public class DocumentOwnershipAuthorizationTests
{
    [Theory]
    [InlineData("Document Owner", true, false)]
    [InlineData("Department Admin", true, false)]
    [InlineData("Document Owner", false, false)]
    [InlineData("Department Admin", false, false)]
    [InlineData("Admin", false, true)]
    public async Task Delete_AllowsOwnerOrManagePermissionOnly(string roleName, bool isOwner, bool hasManagePermission)
    {
        var fixture = await CreateFixtureAsync(roleName, isOwner, hasManagePermission);
        var result = await fixture.Controller.Delete(fixture.Document.DocumentId);

        if (isOwner || hasManagePermission)
        {
            Assert.IsType<OkObjectResult>(result); // deletes answer 200 OK with a message (SafeDeleteAsync)
            fixture.Repository.Verify(x => x.DeleteDocumentAsync(fixture.Document.DocumentId), Times.Once);
        }
        else
        {
            Assert.IsType<ForbidResult>(result);
            fixture.Repository.Verify(x => x.DeleteDocumentAsync(It.IsAny<int>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("Document Owner", true, false)]
    [InlineData("Department Admin", true, false)]
    [InlineData("Document Owner", false, false)]
    [InlineData("Department Admin", false, false)]
    [InlineData("Admin", false, true)]
    public async Task Update_AllowsOwnerOrManagePermissionOnly(string roleName, bool isOwner, bool hasManagePermission)
    {
        var fixture = await CreateFixtureAsync(roleName, isOwner, hasManagePermission);
        var result = await fixture.Controller.Update(fixture.Document.DocumentId, new UploadDocumentDto
        {
            DocumentTypeId = fixture.Document.DocumentTypeId,
            IsCertified = false
        });

        if (isOwner || hasManagePermission)
        {
            Assert.IsType<OkObjectResult>(result);
            fixture.Repository.Verify(x => x.UpdateDocumentAsync(fixture.Document), Times.Once);
        }
        else
        {
            Assert.IsType<ForbidResult>(result);
            fixture.Repository.Verify(x => x.UpdateDocumentAsync(It.IsAny<Document>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("Document Owner", true, false)]
    [InlineData("Department Admin", true, false)]
    [InlineData("Document Owner", false, false)]
    [InlineData("Department Admin", false, false)]
    [InlineData("Admin", false, true)]
    public async Task RevokeDocumentAccess_AllowsOwnerOrManagePermissionOnly(string roleName, bool isOwner, bool hasManagePermission)
    {
        var fixture = await CreateFixtureAsync(roleName, isOwner, hasManagePermission);
        var approval = new DocumentAccessApproval
        {
            ApprovalId = 501,
            EnquiryRequestId = 901,
            DocumentId = fixture.Document.DocumentId,
            ApprovedByUserId = "current-user",
            ApprovedAt = DateTime.UtcNow,
            IsRevoked = false
        };
        fixture.Context.DocumentAccessApprovals.Add(approval);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Controller.RevokeDocumentAccess(fixture.Document.DocumentId, approval.ApprovalId);

        if (isOwner || hasManagePermission)
        {
            Assert.IsType<NoContentResult>(result);
            Assert.True((await fixture.Context.DocumentAccessApprovals.FindAsync(approval.ApprovalId))!.IsRevoked);
        }
        else
        {
            Assert.IsType<ForbidResult>(result);
            Assert.False((await fixture.Context.DocumentAccessApprovals.FindAsync(approval.ApprovalId))!.IsRevoked);
        }
    }

    private static async Task<Fixture> CreateFixtureAsync(string roleName, bool isOwner, bool hasManagePermission)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentOwnershipAuthorizationTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        var currentUser = new User { Id = "current-user", UserName = "current@test.local" };
        var document = new Document
        {
            DocumentId = 700,
            UserId = isOwner ? currentUser.Id : "other-user",
            FileName = "owned.pdf",
            DocumentTypeId = 901,
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            IsCertified = false
        };
        context.Users.Add(currentUser);
        context.Documents.Add(document);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 901, TypeName = "Test Document" });
        context.InstitutionEnquiryRequests.Add(new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 901,
            TargetUserId = currentUser.Id,
            Status = "Pending",
            InstitutionId = 1
        });
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = 901,
            DocumentTypeId = 901,
            FICARuleId = 1,
            isMandatory = true
        });
        if (hasManagePermission)
        {
            context.Permissions.Add(new Permission { PermissionId = 901, PermissionKey = "Documents.Manage" });
            context.RolePermissions.Add(new RolePermission { RoleId = "manage-role", PermissionId = 901 });
            context.UserRoles.Add(new UserRole { UserId = currentUser.Id, RoleId = "manage-role" });
        }
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
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(currentUser);

        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetDocumentByIdAsync(document.DocumentId)).ReturnsAsync(document);
        repository.Setup(x => x.DeleteDocumentAsync(document.DocumentId)).ReturnsAsync(document);
        var service = new Mock<IDocumentService>();
        var compliance = new Mock<IComplianceService>();
        var audit = new Mock<IAuditLogService>();
        var controller = new DocumentController(service.Object, repository.Object, userManager.Object, context, compliance.Object, audit.Object, NullLogger<DocumentController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        return new Fixture(controller, repository, document, context);
    }

    private sealed record Fixture(
        DocumentController Controller,
        Mock<IDocumentRepository> Repository,
        Document Document,
        AppDbContext Context);
}
