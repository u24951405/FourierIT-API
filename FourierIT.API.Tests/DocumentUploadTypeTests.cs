using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
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
/// Which document types a user may upload. Their own required documents never need an institution request,
/// and a pending request must not stop them uploading the rest.
/// </summary>
public class DocumentUploadTypeTests
{
    private const int RequestedTypeId = 991;
    private const int RequiredTypeId = 992;
    private const int OtherTypeId = 993;

    [Fact]
    public async Task Owner_WithAPendingRequest_CanStillUploadAnotherRequiredDocument()
    {
        await using var context = await CreateContextAsync();
        var owner = new User { Id = "upload-owner", UserName = "upload-owner@test.com", EntityTypeId = 990 };
        context.Users.Add(owner);
        context.InstitutionEnquiryRequests.Add(new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 990,
            InstitutionId = 990,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Proof of address",
            RequestDate = DateTimeOffset.UtcNow
        });
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = 990, DocumentTypeId = RequestedTypeId, FICARuleId = 0, isMandatory = true
        });
        await context.SaveChangesAsync();

        Assert.IsType<CreatedAtActionResult>(await CreateController(context, owner).Upload(Upload(RequiredTypeId)));
        Assert.IsType<CreatedAtActionResult>(await CreateController(context, owner).Upload(Upload(RequestedTypeId)));
        Assert.IsType<BadRequestObjectResult>(await CreateController(context, owner).Upload(Upload(OtherTypeId)));
    }

    [Fact]
    public async Task DepartmentAdmin_WithoutAnyRequest_CanUploadTheDepartmentsDocuments()
    {
        await using var context = await CreateContextAsync();
        var admin = new User { Id = "upload-dept-admin", UserName = "upload-dept-admin@test.com", DepartmentId = 990 };
        context.Users.Add(admin);
        context.DepartmentDocumentTypes.Add(new DepartmentDocumentType { DepartmentId = 990, DocumentTypeId = RequiredTypeId, IsMandatory = true });
        await context.SaveChangesAsync();

        Assert.IsType<CreatedAtActionResult>(await CreateController(context, admin, "Department Admin").Upload(Upload(RequiredTypeId)));
        Assert.IsType<BadRequestObjectResult>(await CreateController(context, admin, "Department Admin").Upload(Upload(OtherTypeId)));
    }

    private static UploadDocumentDto Upload(int documentTypeId)
    {
        var bytes = new byte[] { 1, 2, 3 };
        return new UploadDocumentDto
        {
            DocumentTypeId = documentTypeId,
            CertificationDate = DateTime.UtcNow.Date.AddDays(-1),
            File =new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", $"type-{documentTypeId}.pdf")
        };
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentUploadTypeTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        context.DocumentTypes.AddRange(
            new DocumentType { DocumentTypeId = RequestedTypeId, TypeName = "Requested", Description = "Requested" },
            new DocumentType { DocumentTypeId = RequiredTypeId, TypeName = "Required", Description = "Required" },
            new DocumentType { DocumentTypeId = OtherTypeId, TypeName = "Other", Description = "Other" });
        context.EntityTypes.Add(new EntityType { EntityTypeId = 990, Name = "Upload Test Entity" });
        context.RequiredDocuments.Add(new RequiredDocument { EntityTypeId = 990, DocumentTypeId = RequiredTypeId, IsMandatory = true });
        await context.SaveChangesAsync();
        return context;
    }

    private static DocumentController CreateController(AppDbContext context, User currentUser, params string[] roles)
    {
        var userStore = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        userManager.Setup(x => x.IsInRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync((User _, string role) => roles.Contains(role));

        var documentService = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        // Like the real service, the upload saves the document before the controller adds to it.
        documentService
            .Setup(s => s.UploadDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>()))
            .Returns(async (string userId, string fileName, byte[] _, int typeId, DateTimeOffset? _) =>
            {
                var document = new Document { UserId = userId, FileName = fileName, DocumentTypeId = typeId, CurrentStatus = "Uploaded" };
                context.Documents.Add(document);
                await context.SaveChangesAsync();
                return document;
            });

        var controller = new DocumentController(
            documentService.Object,
            new FourierIT_API.Repositories.DocumentRepository(context),
            userManager.Object,
            context,
            new Mock<FourierIT_API.Interfaces.IComplianceService>().Object,
            new Mock<FourierIT_API.Interfaces.IAuditLogService>().Object,
            NullLogger<DocumentController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        return controller;
    }
}
