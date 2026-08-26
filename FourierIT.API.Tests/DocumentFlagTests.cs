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
/// An institution flagging an approved document (FlagApprovedDocument in
/// DocumentAccessRequestController) previously wrote an EnquiryFlag row that nothing ever
/// read back — the owner had no way to see it. These tests cover the two endpoints that
/// close that gap: listing flags across the owner's documents, and marking one resolved.
/// </summary>
public class DocumentFlagTests
{
    [Fact]
    public async Task GetMyDocumentFlags_ReturnsFlagsForOwnersDocumentsWithInstitutionName()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentFlagTests_List_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var owner = new User { Id = "owner-1", UserName = "owner1@test.com" };
        var documentType = new DocumentType { DocumentTypeId = 901, TypeName = "South African ID Book" };
        var document = new Document
        {
            DocumentId = 1,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Absa Corporate Treasury" };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TargetUserId = owner.Id,
            Status = "Approved",
            PurposeNote = "FICA check"
        };
        var flag = new EnquiryFlag
        {
            EnquiryFlagId = 1,
            DocumentId = document.DocumentId,
            EnquiryId = request.EnquiryRequestId,
            FlagReason = "This document appears to be blank.",
            IsResolved = false,
            FlaggedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(owner);
        context.DocumentTypes.Add(documentType);
        context.Institutions.Add(institution);
        context.Documents.Add(document);
        context.InstitutionEnquiryRequests.Add(request);
        context.EnquiryFlags.Add(flag);
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.GetMyDocumentFlags();

        var ok = Assert.IsType<OkObjectResult>(result);
        var flags = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        var flagDto = Assert.Single(flags);
        var type = flagDto.GetType();
        Assert.Equal("Absa Corporate Treasury", type.GetProperty("InstitutionName")!.GetValue(flagDto));
        Assert.Equal("This document appears to be blank.", type.GetProperty("FlagReason")!.GetValue(flagDto));
        Assert.Equal(false, type.GetProperty("IsResolved")!.GetValue(flagDto));
        Assert.Equal(document.DocumentId, type.GetProperty("DocumentId")!.GetValue(flagDto));
    }

    [Fact]
    public async Task GetMyDocumentFlags_DoesNotReturnFlagsForOtherUsersDocuments()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentFlagTests_Isolation_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var owner = new User { Id = "owner-2", UserName = "owner2@test.com" };
        var otherUser = new User { Id = "other-1", UserName = "other1@test.com" };
        var documentType = new DocumentType { DocumentTypeId = 902, TypeName = "Utility Bill" };
        var otherDocument = new Document
        {
            DocumentId = 2,
            UserId = otherUser.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "bill.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        context.Users.AddRange(owner, otherUser);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(otherDocument);
        context.EnquiryFlags.Add(new EnquiryFlag
        {
            EnquiryFlagId = 2,
            DocumentId = otherDocument.DocumentId,
            EnquiryId = 999,
            FlagReason = "Not this owner's business.",
            IsResolved = false,
            FlaggedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.GetMyDocumentFlags();

        var ok = Assert.IsType<OkObjectResult>(result);
        var flags = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.Empty(flags);
    }

    [Fact]
    public async Task ResolveDocumentFlag_ByOwner_MarksFlagResolved()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentFlagTests_Resolve_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var owner = new User { Id = "owner-3", UserName = "owner3@test.com" };
        var documentType = new DocumentType { DocumentTypeId = 903, TypeName = "Passport" };
        var document = new Document
        {
            DocumentId = 3,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "passport.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var flag = new EnquiryFlag
        {
            EnquiryFlagId = 3,
            DocumentId = document.DocumentId,
            EnquiryId = 1,
            FlagReason = "Please re-upload a clearer scan.",
            IsResolved = false,
            FlaggedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(owner);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(document);
        context.EnquiryFlags.Add(flag);
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.ResolveDocumentFlag(document.DocumentId, flag.EnquiryFlagId);

        Assert.IsType<NoContentResult>(result);
        var updated = await context.EnquiryFlags.SingleAsync(f => f.EnquiryFlagId == flag.EnquiryFlagId);
        Assert.True(updated.IsResolved);
    }

    [Fact]
    public async Task ResolveDocumentFlag_ByNonOwnerWithoutManagePermission_ReturnsForbidden()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentFlagTests_Forbidden_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var owner = new User { Id = "owner-4", UserName = "owner4@test.com" };
        var strangerUser = new User { Id = "stranger-1", UserName = "stranger1@test.com" };
        var documentType = new DocumentType { DocumentTypeId = 904, TypeName = "Smart ID Card" };
        var document = new Document
        {
            DocumentId = 4,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var flag = new EnquiryFlag
        {
            EnquiryFlagId = 4,
            DocumentId = document.DocumentId,
            EnquiryId = 1,
            FlagReason = "Reason.",
            IsResolved = false,
            FlaggedAt = DateTimeOffset.UtcNow
        };
        context.Users.AddRange(owner, strangerUser);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(document);
        context.EnquiryFlags.Add(flag);
        await context.SaveChangesAsync();

        var controller = CreateController(context, strangerUser);

        var result = await controller.ResolveDocumentFlag(document.DocumentId, flag.EnquiryFlagId);

        Assert.IsType<ForbidResult>(result);
        var unchanged = await context.EnquiryFlags.SingleAsync(f => f.EnquiryFlagId == flag.EnquiryFlagId);
        Assert.False(unchanged.IsResolved);
    }

    private static DocumentController CreateController(AppDbContext context, User currentUser)
    {
        var userStore = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null, NullLogger<UserManager<User>>.Instance);
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);

        var documentRepository = new FourierIT_API.Repositories.DocumentRepository(context);
        var documentService = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var complianceService = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogService = new Mock<FourierIT_API.Interfaces.IAuditLogService>();

        var controller = new DocumentController(
            documentService.Object,
            documentRepository,
            userManager.Object,
            context,
            complianceService.Object,
            auditLogService.Object,
            NullLogger<DocumentController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        return controller;
    }
}
