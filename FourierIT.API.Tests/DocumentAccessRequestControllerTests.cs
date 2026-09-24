using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentAccessRequestControllerTests
{
    [Fact]
    public async Task CreateInstitutionRequest_AllowsDepartmentFromDifferentInstitution()
    {
        await using var context = CreateContext();
        var requestingInstitution = new Institution { InstitutionId = 15, InstitutionName = "Requesting Institution" };
        var departmentInstitution = new Institution { InstitutionId = 16, InstitutionName = "Department Branch Institution" };
        var branch = new Branch { BranchId = 501, InstitutionId = departmentInstitution.InstitutionId, Institution = departmentInstitution, BranchName = "Internal Branch" };
        var department = new Department { DepartmentId = 601, DepartmentName = "Internal Department", BranchId = branch.BranchId, Branch = branch };
        var documentType = new DocumentType { DocumentTypeId = 901, TypeName = "Identity Document" };
        var departmentUser = CreateUser("department-user", "Department", "User");
        departmentUser.DepartmentId = department.DepartmentId;
        var document = new Document
        {
            DocumentId = 701,
            UserId = departmentUser.Id,
            User = departmentUser,
            DocumentTypeId = documentType.DocumentTypeId,
            DocumentType = documentType,
            FileName = "identity.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };

        context.Institutions.AddRange(requestingInstitution, departmentInstitution);
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
        context.Users.Add(departmentUser);
        context.Documents.Add(document);
        context.InstitutionSessionTokens.Add(CreateSession(requestingInstitution.InstitutionId, "cross-institution-session"));
        await context.SaveChangesAsync();

        var result = await CreateController(context).CreateInstitutionRequest(
            "cross-institution-session",
            new FourierIT_API.DTOs.Document.InstitutionDocumentRequestDto
            {
                RequestType = "Department",
                TargetDepartmentId = department.DepartmentId,
                PurposeNote = "Request internal department documents",
                RequestedDocuments = new List<FourierIT_API.DTOs.Document.RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = documentType.DocumentTypeId, IsMandatory = true }
                }
            });

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(response.Value);
        Assert.Equal(1, await context.InstitutionEnquiryRequests.CountAsync());
        Assert.Equal(1, await context.InstitutionRequestedDocumentTypes.CountAsync());
    }

    [Fact]
    public async Task GetInstitutionUsers_ReturnsOnlyUsersWithDocumentOwnerRole()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution" };
        var documentOwnerRole = await context.Roles.SingleAsync(role => role.Id == "DO");
        var otherRole = new Role { Id = "USER", Name = "User", NormalizedName = "USER" };
        var owner = CreateUser("owner", "Owner", "Person");
        var nonOwner = CreateUser("non-owner", "Other", "Person");

        context.Institutions.Add(institution);
        context.Roles.Add(otherRole);
        context.Users.AddRange(owner, nonOwner);
        context.Set<IdentityUserRole<string>>().Add(new IdentityUserRole<string> { UserId = owner.Id, RoleId = documentOwnerRole.Id });
        context.Set<IdentityUserRole<string>>().Add(new IdentityUserRole<string> { UserId = nonOwner.Id, RoleId = otherRole.Id });
        context.InstitutionSessionTokens.Add(CreateSession(institution.InstitutionId, "session-token"));
        await context.SaveChangesAsync();

        var result = await CreateController(context).GetInstitutionUsers("session-token");

        var response = Assert.IsType<OkObjectResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<object>>(response.Value);
        var userIds = users
            .Select(user => user.GetType().GetProperty("userId")!.GetValue(user)!.ToString())
            .ToList();

        Assert.Equal([owner.Id], userIds);
    }

    [Fact]
    public async Task GetInstitutionUsers_WhenDocumentOwnerRoleIsMissing_ReturnsServerErrorInsteadOfAllUsers()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 2, InstitutionName = "Test Institution" };
        var user = CreateUser("ordinary-user", "Ordinary", "User");

        var seededDocumentOwnerRole = await context.Roles.SingleAsync(role => role.Id == "DO");
        context.Roles.Remove(seededDocumentOwnerRole);
        context.Institutions.Add(institution);
        context.Users.Add(user);
        context.InstitutionSessionTokens.Add(CreateSession(institution.InstitutionId, "session-token"));
        await context.SaveChangesAsync();

        var result = await CreateController(context).GetInstitutionUsers("session-token");

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, response.StatusCode);
        var error = response.Value?.GetType().GetProperty("error")?.GetValue(response.Value)?.ToString();
        Assert.Equal("Document Owner role is not configured.", error);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentAccessRequestControllerTests_{Guid.NewGuid():N}")
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

    private static InstitutionSessionToken CreateSession(int institutionId, string token) => new()
    {
        InstitutionId = institutionId,
        TokenString = token,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        IsRevoked = false
    };

    private static DocumentAccessRequestsController CreateController(AppDbContext context)
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
        var documentService = new Mock<IDocumentService>();
        var departmentValidation = new FourierIT_API.Services.DepartmentRequestValidationService();
        var auditLog = new Mock<IAuditLogService>();

        return new DocumentAccessRequestsController(
            context,
            userManager.Object,
            documentService.Object,
            departmentValidation,
            auditLog.Object);
    }
}
