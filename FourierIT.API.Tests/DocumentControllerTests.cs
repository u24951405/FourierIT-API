using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentControllerTests
{
    [Fact]
    public async Task GetDocumentTypesForMyEntity_DepartmentAdmin_ReturnsDepartmentAssignedDocumentTypes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_DepartmentAssigned_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "admin-2", UserName = "admin2@test.com", DepartmentId = 2 };
        context.Users.Add(user);

        var departmentDocTypeCorporate = new DocumentType { DocumentTypeId = 90021, TypeName = "Certificate of Incorporation" };
        var departmentDocTypeDepartmentSpecific = new DocumentType { DocumentTypeId = 90022, TypeName = "Bank Statement" };

        var department = new Department { DepartmentId = 2, DepartmentName = "Finance Department", BranchId = 1 };
        context.Departments.Add(department);

        context.DocumentTypes.AddRange(departmentDocTypeCorporate, departmentDocTypeDepartmentSpecific);
        context.DepartmentDocumentTypes.AddRange(
            new DepartmentDocumentType { DepartmentDocumentTypeId = 10, DepartmentId = 2, DocumentTypeId = 90021, IsMandatory = true, DocumentType = departmentDocTypeCorporate },
            new DepartmentDocumentType { DepartmentDocumentTypeId = 11, DepartmentId = 2, DocumentTypeId = 90022, IsMandatory = false, DocumentType = departmentDocTypeDepartmentSpecific }
        );

        context.RequiredDocuments.Add(new RequiredDocument
        {
            RequiredDocumentId = 90002,
            EntityTypeId = 3,
            DocumentTypeId = 90021,
            IsMandatory = true,
            Description = "Certificate of incorporation required"
        });

        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(x => x.IsInRoleAsync(user, "Department Admin"))
            .ReturnsAsync(true);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogServiceMock = new Mock<FourierIT_API.Interfaces.IAuditLogService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, auditLogServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.GetDocumentTypesForMyEntity();

        Assert.IsType<OkObjectResult>(result);

        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);

        var response = okResult!.Value;
        var documentTypesProperty = response.GetType().GetProperty("DocumentTypes");
        Assert.NotNull(documentTypesProperty);

        var documentTypes = documentTypesProperty!.GetValue(response) as IEnumerable<object>;
        Assert.NotNull(documentTypes);

        var documentTypeNames = documentTypes!
            .Select(d => d.GetType().GetProperty("TypeName")?.GetValue(d)?.ToString())
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();

        Assert.Contains("Certificate of Incorporation", documentTypeNames);
        Assert.Contains("Bank Statement", documentTypeNames);
    }

    [Fact]
    public async Task Delete_WhenDocumentHasAccessApprovals_ReturnsConflictWithApprovals()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_DeleteConflict_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "owner-1", UserName = "owner@test.com" };
        var document = new Document { DocumentId = 100, FileName = "report.pdf", UserId = user.Id, DocumentTypeId = 1, ExpiryDate = DateTimeOffset.UtcNow.AddYears(1), CurrentStatus = "Active", FileSizeBytes = 1234, EncryptionAlgorithm = "AES-256", IsEncrypted = true, IsCertified = false };
        var institution = new Institution { InstitutionId = 500, InstitutionName = "Test Bank" };
        var enquiryRequest = new InstitutionEnquiryRequest { EnquiryRequestId = 700, InstitutionId = institution.InstitutionId, Institution = institution, TargetUserId = user.Id, Status = "Approved" };
        var approval = new DocumentAccessApproval { ApprovalId = 800, DocumentId = document.DocumentId, Document = document, EnquiryRequestId = enquiryRequest.EnquiryRequestId, InstitutionEnquiryRequest = enquiryRequest, ApprovedByUserId = user.Id, ApprovedByUser = user, ApprovedAt = DateTime.UtcNow, IsRevoked = false };

        context.Users.Add(user);
        context.Documents.Add(document);
        context.Institutions.Add(institution);
        context.InstitutionEnquiryRequests.Add(enquiryRequest);
        context.DocumentAccessApprovals.Add(approval);
        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        documentRepositoryMock.Setup(x => x.GetDocumentByIdAsync(document.DocumentId)).ReturnsAsync(document);
        documentRepositoryMock.Setup(x => x.DeleteDocumentAsync(document.DocumentId)).ReturnsAsync(document);

        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogServiceMock = new Mock<FourierIT_API.Interfaces.IAuditLogService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, auditLogServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.Delete(document.DocumentId);

        Assert.IsType<ConflictObjectResult>(result);
        var conflictResult = result as ConflictObjectResult;
        Assert.NotNull(conflictResult?.Value);

        var response = conflictResult!.Value;
        var messageProperty = response.GetType().GetProperty("message");
        var approvalsProperty = response.GetType().GetProperty("approvals");

        Assert.NotNull(messageProperty);
        Assert.NotNull(approvalsProperty);

        var message = messageProperty.GetValue(response) as string;
        var approvals = approvalsProperty.GetValue(response) as IEnumerable<object>;

        Assert.Equal("Document cannot be deleted while access approvals exist.", message);
        Assert.NotNull(approvals);
        Assert.Single(approvals!);
    }

    [Fact]
    public async Task Delete_WhenAllAccessApprovalsAreRevoked_AllowsDeletion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_DeleteRevoked_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "owner-3", UserName = "owner3@test.com" };
        var document = new Document { DocumentId = 101, FileName = "report2.pdf", UserId = user.Id, DocumentTypeId = 1, ExpiryDate = DateTimeOffset.UtcNow.AddYears(1), CurrentStatus = "Active", FileSizeBytes = 1234, EncryptionAlgorithm = "AES-256", IsEncrypted = true, IsCertified = false };
        var institution = new Institution { InstitutionId = 501, InstitutionName = "Test Bank" };
        var enquiryRequest = new InstitutionEnquiryRequest { EnquiryRequestId = 701, InstitutionId = institution.InstitutionId, Institution = institution, TargetUserId = user.Id, Status = "Approved" };
        var approval = new DocumentAccessApproval { ApprovalId = 801, DocumentId = document.DocumentId, Document = document, EnquiryRequestId = enquiryRequest.EnquiryRequestId, InstitutionEnquiryRequest = enquiryRequest, ApprovedByUserId = user.Id, ApprovedByUser = user, ApprovedAt = DateTime.UtcNow, IsRevoked = true, RevokedAt = DateTime.UtcNow };

        context.Users.Add(user);
        context.Documents.Add(document);
        context.Institutions.Add(institution);
        context.InstitutionEnquiryRequests.Add(enquiryRequest);
        context.DocumentAccessApprovals.Add(approval);
        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        documentRepositoryMock.Setup(x => x.GetDocumentByIdAsync(document.DocumentId)).ReturnsAsync(document);
        documentRepositoryMock.Setup(x => x.DeleteDocumentAsync(document.DocumentId)).ReturnsAsync(document);

        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogServiceMock = new Mock<FourierIT_API.Interfaces.IAuditLogService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, auditLogServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.Delete(document.DocumentId);

        Assert.IsType<NoContentResult>(result);
        documentRepositoryMock.Verify(x => x.DeleteDocumentAsync(document.DocumentId), Times.Once);
    }

    [Fact]
    public async Task GetRequiredDocumentsStatus_WhenActiveRequestExists_ReturnsRequestedDocumentChecklist()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_ActiveRequestRequiredDocs_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "owner-2", UserName = "owner2@test.com" };
        var documentTypeRequested = new DocumentType { DocumentTypeId = 200, TypeName = "Utility Bill" };
        var documentTypeUnrequested = new DocumentType { DocumentTypeId = 201, TypeName = "Passport" };
        var institution = new Institution { InstitutionId = 501, InstitutionName = "Test Bank" };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 701,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TargetUserId = user.Id,
            Status = "Pending",
            PurposeNote = "Request documents"
        };

        context.Users.Add(user);
        context.DocumentTypes.AddRange(documentTypeRequested, documentTypeUnrequested);
        context.Institutions.Add(institution);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            InstitutionEnquiryRequest = request,
            DocumentTypeId = documentTypeRequested.DocumentTypeId,
            DocumentType = documentTypeRequested,
            FICARuleId = 1,
            isMandatory = true
        });
        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(x => x.IsInRoleAsync(user, "Department Admin"))
            .ReturnsAsync(false);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogServiceMock = new Mock<FourierIT_API.Interfaces.IAuditLogService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, auditLogServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.GetRequiredDocumentsStatus();

        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);

        var response = okResult!.Value;
        var documentsProperty = response.GetType().GetProperty("Documents");
        Assert.NotNull(documentsProperty);

        var documents = documentsProperty!.GetValue(response) as IEnumerable<object>;
        Assert.NotNull(documents);

        var docList = documents!.ToList();
        Assert.Single(docList);

        var firstDocument = docList.Single();
        var documentTypeName = firstDocument.GetType().GetProperty("DocumentTypeName")?.GetValue(firstDocument)?.ToString();
        var isUploaded = (bool?)firstDocument.GetType().GetProperty("IsUploaded")?.GetValue(firstDocument);

        Assert.Equal("Utility Bill", documentTypeName);
        Assert.False(isUploaded.GetValueOrDefault());
    }

    [Fact]
    public async Task GetDocumentTypesForMyEntity_DepartmentAdmin_ReturnsOnlyAssignedDepartmentDocumentTypes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_Db_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "admin-1", UserName = "admin@test.com", DepartmentId = 1 };
        context.Users.Add(user);

        var documentTypeCorporate = new DocumentType { DocumentTypeId = 90011, TypeName = "Certificate of Incorporation" };
        var unassignedDocumentType = new DocumentType { DocumentTypeId = 90001, TypeName = "South African ID Book" };
        var departmentType = new DepartmentDocumentType { DepartmentDocumentTypeId = 1, DepartmentId = 1, DocumentTypeId = 90011, IsMandatory = true, DocumentType = documentTypeCorporate };
        var requiredDocumentCorporate = new RequiredDocument { RequiredDocumentId = 90001, EntityTypeId = 3, DocumentTypeId = 90011, IsMandatory = true, Description = "Certificate of incorporation required" };

        context.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "Fourier IT Innovation", BranchId = 1 });
        context.DocumentTypes.AddRange(documentTypeCorporate, unassignedDocumentType);
        context.DepartmentDocumentTypes.Add(departmentType);
        context.RequiredDocuments.Add(requiredDocumentCorporate);
        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(x => x.IsInRoleAsync(user, "Department Admin"))
            .ReturnsAsync(true);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var auditLogServiceMock = new Mock<FourierIT_API.Interfaces.IAuditLogService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, auditLogServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.GetDocumentTypesForMyEntity();

        Assert.IsType<OkObjectResult>(result);

        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);

        var response = okResult.Value;
        Assert.NotNull(response);

        var documentTypesProperty = response!.GetType().GetProperty("DocumentTypes");
        Assert.NotNull(documentTypesProperty);

        var documentTypes = documentTypesProperty!.GetValue(response) as IEnumerable<object>;
        Assert.NotNull(documentTypes);

        var documentTypeNames = documentTypes!
            .Select(d => d.GetType().GetProperty("TypeName")?.GetValue(d)?.ToString())
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();
        Assert.Contains("Certificate of Incorporation", documentTypeNames);
        Assert.DoesNotContain("South African ID Book", documentTypeNames);
    }
}
