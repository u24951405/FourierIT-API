using System.Security.Claims;
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
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// An institution must be able to request a document type that the target document owner or
/// department hasn't uploaded yet — the request itself becomes the requirement for them to
/// upload it (surfaced via GetRequiredDocumentsStatus). These endpoints used to hard-reject
/// the request up front instead, which made it impossible to ever request a missing document.
/// </summary>
public class DocumentAccessRequestUploadRequirementTests
{
    [Fact]
    public async Task CreateInstitutionRequest_Individual_TargetHasNotUploadedRequestedDocumentType_StillCreatesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution" };
        var actor = CreateUser("actor-user", "Actor", "Person");
        var targetUser = CreateUser("target-user", "Target", "Owner");
        var documentType = new DocumentType { DocumentTypeId = 901, TypeName = "South African ID Book", Description = "ID document" };
        var sessionToken = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TokenString = "test-session-token-1",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        context.Institutions.Add(institution);
        context.InstitutionSessionTokens.Add(sessionToken);
        context.Users.AddRange(actor, targetUser);
        context.DocumentTypes.Add(documentType);
        await context.SaveChangesAsync();

        var controller = CreateController(context, actor);

        var result = await controller.CreateInstitutionRequest(
            sessionToken.TokenString,
            new InstitutionDocumentRequestDto
            {
                RequestType = "Individual",
                TargetUserId = targetUser.Id,
                PurposeNote = "Please provide your ID",
                RequestedDocuments = new List<RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = documentType.DocumentTypeId, IsMandatory = true }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await context.InstitutionEnquiryRequests.CountAsync());
        Assert.Equal(1, await context.InstitutionRequestedDocumentTypes.CountAsync());
    }

    [Fact]
    public async Task CreateInstitutionRequest_Department_NoMemberHasUploadedRequestedDocumentType_StillCreatesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 2, InstitutionName = "Test Institution 2" };
        var sessionToken = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            TokenString = "session-token-2",
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        var actor = CreateUser("actor-user-2", "Actor", "Person");
        var branch = new Branch { BranchId = 1, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 1, DepartmentName = "Operations", BranchId = branch.BranchId, Branch = branch };
        var documentType = new DocumentType { DocumentTypeId = 902, TypeName = "Utility Bill", Description = "Proof of address" };
        var departmentUser = CreateUser("dept-user", "Dept", "User");
        departmentUser.DepartmentId = department.DepartmentId;

        context.Institutions.Add(institution);
        context.InstitutionSessionTokens.Add(sessionToken);
        context.Users.AddRange(actor, departmentUser);
        context.Branches.Add(branch);
        context.Departments.Add(department);
        context.DocumentTypes.Add(documentType);
        context.DepartmentDocumentTypes.Add(new DepartmentDocumentType
        {
            DepartmentId = department.DepartmentId,
            Department = department,
            DocumentTypeId = documentType.DocumentTypeId,
            DocumentType = documentType,
            IsMandatory = true
        });
        context.InstitutionSessionTokens.Add(sessionToken);
        await context.SaveChangesAsync();

        var controller = CreateController(context, actor);

        var result = await controller.CreateInstitutionRequest(
            sessionToken.TokenString,
            new InstitutionDocumentRequestDto
            {
                RequestType = "Department",
                TargetDepartmentId = department.DepartmentId,
                PurposeNote = "Please provide proof of address",
                RequestedDocuments = new List<RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = documentType.DocumentTypeId, IsMandatory = true }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await context.InstitutionEnquiryRequests.CountAsync());
    }

    [Fact]
    public async Task RouteRequestToOwner_OwnerHasNotUploadedRequestedDocumentType_StillRoutesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 3, InstitutionName = "Test Institution 3" };
        var branch = new Branch { BranchId = 2, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 2, DepartmentName = "Compliance", BranchId = branch.BranchId, Branch = branch };
        var departmentAdmin = CreateUser("dept-admin", "Admin", "Person");
        departmentAdmin.DepartmentId = department.DepartmentId;
        var owner = CreateUser("doc-owner", "Owner", "Person");
        owner.DepartmentId = department.DepartmentId;
        var documentType = new DocumentType { DocumentTypeId = 903, TypeName = "Passport", Description = "Travel document" };

        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = institution.InstitutionId,
            RequestType = "Department",
            TargetDepartmentId = department.DepartmentId,
            Status = "Department_Pending",
            PurposeNote = "Please provide a passport",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Institutions.Add(institution);
        context.Branches.Add(branch);
        context.Departments.Add(department);
        context.Users.AddRange(departmentAdmin, owner);
        context.DocumentTypes.Add(documentType);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            DocumentTypeId = documentType.DocumentTypeId,
            FICARuleId = 0,
            isMandatory = true
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, departmentAdmin);

        var result = await controller.RouteRequestToOwner(
            request.EnquiryRequestId,
            new RouteRequestToOwnerDto { TargetUserId = owner.Id });

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Pending", updated.Status);
        Assert.Equal(owner.Id, updated.TargetUserId);
    }

    [Fact]
    public async Task Approve_WhenNotAllRequestedDocumentTypesAreUploaded_ReturnsBadRequestWithMissingTypes()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-partial", "Owner", "Partial");
        var institutionType = new InstitutionType { InstitutionTypeId = 901, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution", VerifiedDomain = "test.co.za", RegNumber = 1001, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var idType = new DocumentType { DocumentTypeId = 904, TypeName = "South African ID Book", Description = "ID document" };
        var billType = new DocumentType { DocumentTypeId = 905, TypeName = "Utility Bill", Description = "Proof of address" };
        var uploadedDocument = new Document
        {
            DocumentId = 1,
            UserId = owner.Id,
            DocumentTypeId = idType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = 1,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide ID and proof of address",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.AddRange(idType, billType);
        context.Documents.Add(uploadedDocument);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.AddRange(
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = idType.DocumentTypeId, FICARuleId = 0, isMandatory = true },
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = billType.DocumentTypeId, FICARuleId = 0, isMandatory = true });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var missingIds = badRequest.Value!.GetType().GetProperty("missingDocumentTypeIds")!.GetValue(badRequest.Value) as System.Collections.IEnumerable;
        Assert.Contains(billType.DocumentTypeId, missingIds!.Cast<int>());

        var unchanged = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Pending", unchanged.Status);
        Assert.Equal(0, await context.DocumentAccessApprovals.CountAsync());
    }

    [Fact]
    public async Task Approve_WhenAllRequestedDocumentTypesAreUploaded_Succeeds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-complete", "Owner", "Complete");
        var institutionType = new InstitutionType { InstitutionTypeId = 902, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution", VerifiedDomain = "test2.co.za", RegNumber = 1002, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var idType = new DocumentType { DocumentTypeId = 906, TypeName = "South African ID Book", Description = "ID document" };
        var uploadedDocument = new Document
        {
            DocumentId = 2,
            UserId = owner.Id,
            DocumentTypeId = idType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 2,
            InstitutionId = 1,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide ID",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.Add(idType);
        context.Documents.Add(uploadedDocument);
        context.InstitutionEnquiryRequests.Add(request);
        var complianceStatus = new ComplianceStatus { UserId = owner.Id, User = owner };
        context.ComplianceStatuses.Add(complianceStatus);
        context.InstitutionRequestedDocumentTypes.Add(
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = idType.DocumentTypeId, FICARuleId = 0, isMandatory = true });
        await context.SaveChangesAsync();
        context.DocumentComplianceChecks.Add(new DocumentComplianceCheck
        {
            CheckId = 1,
            DocumentId = uploadedDocument.DocumentId,
            ComplianceStatusId = complianceStatus.ComplianceStatusId,
            CheckStatus = "Compliant",
            RequiresManualReview = true,
            IsManuallyApproved = true,
            ManualReviewDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Approved", updated.Status);
        Assert.Equal(1, await context.DocumentAccessApprovals.CountAsync());
    }

    [Fact]
    public async Task Approve_WhenRequestedDocumentHasNotBeenApprovedByCompliance_ReturnsConflict()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-unreviewed", "Owner", "Unreviewed");
        var institutionType = new InstitutionType { InstitutionTypeId = 903, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 3, InstitutionName = "Review Institution", VerifiedDomain = "review.test", RegNumber = 1003, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var documentType = new DocumentType { DocumentTypeId = 907, TypeName = "Proof of Address", Description = "Address document" };
        var document = new Document
        {
            DocumentId = 3,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "address.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 3,
            InstitutionId = institution.InstitutionId,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide proof of address",
            RequestDate = DateTimeOffset.UtcNow
        };
        var status = new ComplianceStatus { UserId = owner.Id, User = owner };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(document);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            DocumentTypeId = documentType.DocumentTypeId,
            FICARuleId = 0,
            isMandatory = true
        });
        context.ComplianceStatuses.Add(status);
        await context.SaveChangesAsync();
        context.DocumentComplianceChecks.Add(new DocumentComplianceCheck
        {
            CheckId = 2,
            DocumentId = document.DocumentId,
            ComplianceStatusId = status.ComplianceStatusId,
            CheckStatus = "Pending Review",
            RequiresManualReview = true,
            IsManuallyApproved = false
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("Compliance Officer", conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value)!.ToString());
        Assert.Equal("Pending", (await context.InstitutionEnquiryRequests.SingleAsync()).Status);
        Assert.Empty(await context.DocumentAccessApprovals.ToListAsync());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentAccessRequestUploadRequirementTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static User CreateUser(string id, string firstName, string lastName)
    {
        var user = new User { Id = id, UserName = $"{id}@test.com", NormalizedUserName = $"{id.ToUpperInvariant()}@TEST.COM" };
        user.Profile = new Profile
        {
            UserId = id,
            User = user,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = "0123456789",
            JobTitle = "Tester"
        };
        return user;
    }

    private static DocumentAccessRequestsController CreateController(AppDbContext context, User actor)
    {
        var userStore = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            userStore.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(actor);
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .Returns<string>(async id => await context.Users.FirstOrDefaultAsync(u => u.Id == id));

        var documentService = new Mock<IDocumentService>();
        var complianceService = new Mock<IComplianceService>();
        complianceService
            .Setup(service => service.CheckUserComplianceAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((string userId, bool _) => new ComplianceStatus { UserId = userId });
        var departmentValidation = new FourierIT_API.Services.DepartmentRequestValidationService();
        var auditLog = new Mock<IAuditLogService>();

        var controller = new DocumentAccessRequestsController(
            context,
            userManager.Object,
            documentService.Object,
            departmentValidation,
            auditLog.Object,
            complianceService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        return controller;
    }
}
