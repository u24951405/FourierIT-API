using FourierIT_API.Data;
using FourierIT_API.Models;
using FourierIT_API.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// DeleteDocumentAsync must clean up every table with a real (non-cascading) foreign key to
/// Documents before removing the row, or SQL Server rejects the delete with a REFERENCE
/// constraint violation. Uses a real Sqlite database with foreign keys enforced — the EF
/// InMemory provider used by DocumentControllerTests mocks IDocumentRepository entirely and
/// would not exercise this method's SQL, nor enforce FK constraints if it did.
/// </summary>
public class DocumentRepositoryDeleteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public DocumentRepositoryDeleteTests()
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
    public async Task DeleteDocumentAsync_WithRevokedAccessApprovalAndComplianceCheck_DeletesWithoutFkViolation()
    {
        var user = new User { Id = "owner-1", UserName = "owner1@test.com" };
        var documentType = new DocumentType { DocumentTypeId = 901, TypeName = "South African ID Book", Description = "ID document" };
        var document = new Document
        {
            DocumentId = 1,
            UserId = user.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var institutionType = new InstitutionType { InstitutionTypeId = 1, InstitutionTypeName = "Bank" };
        var institution = new Institution
        {
            InstitutionId = 1,
            InstitutionName = "Absa Corporate Treasury",
            VerifiedDomain = "absa.co.za",
            RegNumber = 1001,
            TypeId = institutionType.InstitutionTypeId,
            InstitutionType = institutionType
        };
        var enquiryRequest = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = institution.InstitutionId,
            TargetUserId = user.Id,
            Status = "Approved",
            PurposeNote = "FICA check",
            RequestDate = DateTimeOffset.UtcNow
        };
        var revokedApproval = new DocumentAccessApproval
        {
            ApprovalId = 1,
            DocumentId = document.DocumentId,
            EnquiryRequestId = enquiryRequest.EnquiryRequestId,
            ApprovedByUserId = user.Id,
            ApprovedAt = DateTime.UtcNow,
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow
        };
        var complianceRule = new ComplianceRule { ComplianceRuleId = 1, RuleName = "Address max age", AppliesTo = "DocumentType" };
        var complianceCheck = new ComplianceCheck
        {
            ComplianceCheckId = 1,
            ComplianceRuleId = complianceRule.ComplianceRuleId,
            DocumentId = document.DocumentId,
            CheckStatus = "Compliant"
        };

        _context.Users.Add(user);
        _context.DocumentTypes.Add(documentType);
        _context.InstitutionTypes.Add(institutionType);
        _context.Institutions.Add(institution);
        _context.Documents.Add(document);
        _context.InstitutionEnquiryRequests.Add(enquiryRequest);
        _context.DocumentAccessApprovals.Add(revokedApproval);
        _context.ComplianceRules.Add(complianceRule);
        _context.ComplianceChecks.Add(complianceCheck);
        await _context.SaveChangesAsync();

        var repository = new DocumentRepository(_context);
        var deleted = await repository.DeleteDocumentAsync(document.DocumentId);

        Assert.NotNull(deleted);
        Assert.Null(await _context.Documents.FindAsync(document.DocumentId));
        Assert.Null(await _context.DocumentAccessApprovals.FindAsync(revokedApproval.ApprovalId));

        var survivingCheck = await _context.ComplianceChecks.FindAsync(complianceCheck.ComplianceCheckId);
        Assert.NotNull(survivingCheck);
        Assert.Null(survivingCheck!.DocumentId);
    }
}
