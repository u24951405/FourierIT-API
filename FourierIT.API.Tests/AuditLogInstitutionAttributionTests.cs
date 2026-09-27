using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// Regression coverage for the institution-actor audit trail: an institution acting
/// through the portal has no AspNetUsers row, so AuditLogs.UserId must stay null and
/// the actor must be recorded via AuditLogs.InstitutionId instead. These tests use a
/// real Sqlite database (with foreign keys enforced) and the real AuditLogService,
/// not mocks, because the EF InMemory provider used elsewhere in this test project
/// does not enforce foreign key constraints and would not have caught this bug.
/// </summary>
public class AuditLogInstitutionAttributionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public AuditLogInstitutionAttributionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateInstitutionRequest_FromAnonymousPortalSession_WritesAuditLogWithoutFkViolation()
    {
        var institution = SeedInstitution(15, "Absa Corporate Treasury");
        var department = SeedDepartment(institution, 5, "Demo 1-1 Operations", requiredDocumentTypeId: 1);
        var session = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            TokenString = "absa-session-token",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsRevoked = false
        };
        _context.InstitutionSessionTokens.Add(session);
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithRealAuditLog();

        var result = await controller.CreateInstitutionRequest(
            "absa-session-token",
            new InstitutionDocumentRequestDto
            {
                RequestType = "Department",
                TargetDepartmentId = department.DepartmentId,
                PurposeNote = "FICA compliance verification",
                RequestedDocuments = new List<RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = 1, IsMandatory = true }
                }
            });

        Assert.IsType<OkObjectResult>(result);

        var auditEntry = await _context.AuditLogs
            .SingleAsync(a => a.ActionCode == "INSTITUTION_REQUEST_CREATED");

        Assert.Null(auditEntry.UserId);
        Assert.Equal(institution.InstitutionId, auditEntry.InstitutionId);
    }

    [Fact]
    public async Task SeededDepartmentAdminRole_IncludesAuditViewPermission()
    {
        var permission = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == "DA")
            .Select(rp => rp.Permission.PermissionKey)
            .ToListAsync();

        Assert.Contains("Audit.View", permission);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_InstitutionAttributedEntry_DisplaysInstitutionNameWithNullUser()
    {
        // Uses the InMemory provider (like the rest of this test project) rather than the
        // Sqlite fixture above: this test is only about the display/search projection, and
        // Sqlite cannot translate ORDER BY over DateTimeOffset, which AuditLogService relies on.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuditLogDisplayTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var institutionType = new InstitutionType { InstitutionTypeId = 1, InstitutionTypeName = "Bank" };
        var institution = new Institution
        {
            InstitutionId = 20,
            InstitutionName = "First National Trust",
            VerifiedDomain = "fnt.co.za",
            RegNumber = 1020,
            TypeId = institutionType.InstitutionTypeId,
            InstitutionType = institutionType
        };
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.AuditLogs.Add(new AuditLog
        {
            UserId = null,
            InstitutionId = institution.InstitutionId,
            ActionCode = "INSTITUTION_REQUEST_CREATED",
            TimeStamp = DateTimeOffset.UtcNow,
            Description = "Institution portal request 42 created.",
            TableAffected = "InstitutionEnquiryRequests",
            RecordID = 42
        });
        await context.SaveChangesAsync();

        var service = new AuditLogService(context);

        var paged = await service.GetAuditLogsPagedAsync(null, null, null, null, page: 1, pageSize: 50, query: null);
        var entry = Assert.Single(paged.Items);
        Assert.Null(entry.UserId);
        Assert.Equal(institution.InstitutionId, entry.InstitutionId);
        Assert.Equal("First National Trust", entry.InstitutionName);

        // The free-text search used by the audit-log screen should also match on institution name.
        var searched = await service.GetAuditLogsPagedAsync(null, null, null, null, page: 1, pageSize: 50, query: "first national");
        Assert.Single(searched.Items);
    }

    private Institution SeedInstitution(int institutionId, string name)
    {
        var institutionType = _context.InstitutionTypes.FirstOrDefault(t => t.InstitutionTypeId == 1);
        if (institutionType == null)
        {
            institutionType = new InstitutionType { InstitutionTypeId = 1, InstitutionTypeName = "Bank" };
            _context.InstitutionTypes.Add(institutionType);
        }

        var institution = new Institution
        {
            InstitutionId = institutionId,
            InstitutionName = name,
            VerifiedDomain = $"{name.Replace(" ", "").ToLowerInvariant()}.co.za",
            RegNumber = 1000 + institutionId,
            TypeId = institutionType.InstitutionTypeId,
            InstitutionType = institutionType
        };
        _context.Institutions.Add(institution);
        _context.SaveChanges();
        return institution;
    }

    private Department SeedDepartment(Institution institution, int departmentId, string name, int requiredDocumentTypeId)
    {
        var branch = new Branch
        {
            BranchId = departmentId + 500,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            BranchName = $"{name} Branch"
        };
        var department = new Department
        {
            DepartmentId = departmentId,
            DepartmentName = name,
            BranchId = branch.BranchId,
            Branch = branch
        };
        var documentType = _context.DocumentTypes.FirstOrDefault(dt => dt.DocumentTypeId == requiredDocumentTypeId);
        if (documentType == null)
        {
            documentType = new DocumentType
            {
                DocumentTypeId = requiredDocumentTypeId,
                TypeName = "South African ID Book",
                Description = "Government-issued identity document"
            };
            _context.DocumentTypes.Add(documentType);
        }

        _context.Branches.Add(branch);
        _context.Departments.Add(department);
        _context.DepartmentDocumentTypes.Add(new DepartmentDocumentType
        {
            DepartmentId = department.DepartmentId,
            Department = department,
            DocumentTypeId = documentType.DocumentTypeId,
            DocumentType = documentType,
            IsMandatory = true
        });
        _context.SaveChanges();
        return department;
    }

    private DocumentAccessRequestsController CreateControllerWithRealAuditLog()
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
        var departmentValidation = new DepartmentRequestValidationService();
        IAuditLogService auditLogService = new AuditLogService(_context);
        var compliance = new Mock<IComplianceService>();

        return new DocumentAccessRequestsController(
            _context,
            userManager.Object,
            documentService.Object,
            departmentValidation,
            auditLogService,
            compliance.Object);
    }
}
