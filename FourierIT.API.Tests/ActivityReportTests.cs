using System.Text.Json;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Reports;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT.API.Tests;

public class ActivityReportTests
{
    [Fact]
    public async Task GetActivityReport_ReturnsRealOwnerDocumentActivity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ActivityReportTests_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);

        var owner = new User
        {
            Id = "owner-1",
            UserName = "owner1@example.com",
            Email = "owner1@example.com",
            Profile = new Profile
            {
                FirstName = "Jane",
                LastName = "Owner",
                DateOfBirth = new DateOnly(1990, 1, 15),
                PhoneNumber = "0810000000",
                JobTitle = "Document Owner",
                UserId = "owner-1"
            }
        };

        var accessor = new User
        {
            Id = "accessor-1",
            UserName = "accessor1@example.com",
            Email = "accessor1@example.com",
            Profile = new Profile
            {
                FirstName = "Ava",
                LastName = "Accessor",
                DateOfBirth = new DateOnly(1988, 5, 23),
                PhoneNumber = "0820000000",
                JobTitle = "Compliance Officer",
                UserId = "accessor-1"
            }
        };

        var identityType = new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" };

        var docOne = new Document
        {
            DocumentId = 101,
            FileName = "id-passport.pdf",
            UserId = owner.Id,
            DocumentTypeId = identityType.DocumentTypeId,
            DocumentType = identityType,
            CurrentStatus = "Verified",
            UploadedDate = new DateTime(2026, 1, 12, 9, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2027, 1, 12, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 2048,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            IsCertified = true,
            User = owner
        };

        var docTwo = new Document
        {
            DocumentId = 102,
            FileName = "proof-of-address.pdf",
            UserId = owner.Id,
            DocumentTypeId = 2,
            DocumentType = new DocumentType { DocumentTypeId = 2, TypeName = "Proof of Residence" },
            CurrentStatus = "Pending",
            UploadedDate = new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 1024,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            IsCertified = false,
            User = owner
        };

        var institution = new Institution
        {
            InstitutionId = 10,
            InstitutionName = "Test Bank",
            VerifiedDomain = "testbank.com",
            RegNumber = 123456,
            TypeId = 1,
            InstitutionType = new InstitutionType { InstitutionTypeId = 1, InstitutionTypeName = "Bank" }
        };

        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 55,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TargetUserId = owner.Id,
            TargetUser = owner,
            Status = "Approved",
            PurposeNote = "KYC review",
            RequestDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            ReferenceNumber = "REQ-1001"
        };

        var access = new DocumentAccess
        {
            DocumentAccessId = 1,
            DocumentId = docOne.DocumentId,
            Document = docOne,
            GrantedToUserId = accessor.Id,
            GrantedToUser = accessor,
            AccessLevel = AccessLevel.View,
            GrantedDate = new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc),
            Reason = "Approval for bank review"
        };

        var approval = new DocumentAccessApproval
        {
            ApprovalId = 1,
            EnquiryRequestId = request.EnquiryRequestId,
            InstitutionEnquiryRequest = request,
            DocumentId = docOne.DocumentId,
            Document = docOne,
            ApprovedByUserId = owner.Id,
            ApprovedByUser = owner,
            ApprovedAt = new DateTime(2026, 2, 11, 8, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2027, 2, 11, 8, 0, 0, DateTimeKind.Utc),
            IsRevoked = false
        };

        var log = new DocumentAccessLog
        {
            DocumentAccessLogId = 1,
            DocumentId = docOne.DocumentId,
            Document = docOne,
            AccessedByUserId = accessor.Id,
            AccessedByUser = accessor,
            ActionType = "VIEW",
            AccessDateTime = new DateTime(2026, 2, 12, 10, 0, 0, DateTimeKind.Utc),
            IPAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0"
        };

        context.Users.AddRange(owner, accessor);
        context.DocumentTypes.AddRange(identityType, docTwo.DocumentType);
        context.Documents.AddRange(docOne, docTwo);
        context.Institutions.Add(institution);
        context.InstitutionEnquiryRequests.Add(request);
        context.DocumentAccesses.Add(access);
        context.DocumentAccessApprovals.Add(approval);
        context.DocumentAccessLogs.Add(log);

        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var result = await controller.GetActivityReport(owner.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ActivityReportDto>(ok.Value);

        Assert.Equal("Jane Owner", dto.DocumentOwner);
        Assert.Equal("owner-1", dto.OwnerId);
        Assert.Equal(2, dto.TotalDocuments);
        Assert.Equal(1, dto.ActiveDocuments);
        Assert.Equal(1, dto.InactiveDocuments);
        Assert.Contains(dto.DistributionByCategory, x => x.Label == "Identity Document" && x.Count == 1);
        Assert.Contains(dto.Inventory, x => x.DocumentName == "id-passport.pdf" && x.VerificationStatus == "Verified");
        Assert.Contains(dto.VaultAccessLog, x => x.AccessorName == "Ava Accessor" && x.ActionReason.Contains("VIEW"));
        Assert.Contains(dto.ClientRelationships, x => x.Organisation == "Test Bank" && x.DocumentsShared == 1);
    }

    [Fact]
    public async Task GetActivityReport_ReturnsDeterministicDataAcrossRepeatedCalls()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ActivityReportDeterministic_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);

        var owner = new User
        {
            Id = "owner-deterministic",
            UserName = "owner.det@example.com",
            Email = "owner.det@example.com",
            Profile = new Profile
            {
                FirstName = "Det",
                LastName = "Owner",
                DateOfBirth = new DateOnly(1990, 2, 14),
                PhoneNumber = "0831111111",
                JobTitle = "Analyst",
                UserId = "owner-deterministic"
            }
        };

        var accessor = new User
        {
            Id = "accessor-deterministic",
            UserName = "accessor.det@example.com",
            Email = "accessor.det@example.com",
            Profile = new Profile
            {
                FirstName = "Ava",
                LastName = "Accessor",
                DateOfBirth = new DateOnly(1986, 4, 18),
                PhoneNumber = "0841111111",
                JobTitle = "Compliance Officer",
                UserId = "accessor-deterministic"
            }
        };

        var identityType = new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" };

        var document = new Document
        {
            DocumentId = 201,
            FileName = "deterministic-passport.pdf",
            UserId = owner.Id,
            DocumentTypeId = identityType.DocumentTypeId,
            DocumentType = identityType,
            CurrentStatus = "Verified",
            UploadedDate = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2027, 5, 12, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 2048,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            IsCertified = true,
            User = owner
        };

        var institution = new Institution
        {
            InstitutionId = 20,
            InstitutionName = "Deterministic Bank",
            VerifiedDomain = "detbank.com",
            RegNumber = 777777,
            TypeId = 1,
            InstitutionType = new InstitutionType { InstitutionTypeId = 1, InstitutionTypeName = "Bank" }
        };

        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 220,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TargetUserId = owner.Id,
            TargetUser = owner,
            Status = "Approved",
            PurposeNote = "Review for deterministic test",
            RequestDate = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            ReferenceNumber = "REQ-Det-01"
        };

        var approval = new DocumentAccessApproval
        {
            ApprovalId = 2,
            EnquiryRequestId = request.EnquiryRequestId,
            InstitutionEnquiryRequest = request,
            DocumentId = document.DocumentId,
            Document = document,
            ApprovedByUserId = owner.Id,
            ApprovedByUser = owner,
            ApprovedAt = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2027, 5, 2, 8, 0, 0, DateTimeKind.Utc),
            IsRevoked = false
        };

        var log = new DocumentAccessLog
        {
            DocumentAccessLogId = 2,
            DocumentId = document.DocumentId,
            Document = document,
            AccessedByUserId = accessor.Id,
            AccessedByUser = accessor,
            ActionType = "VIEW",
            AccessDateTime = new DateTime(2026, 5, 13, 11, 0, 0, DateTimeKind.Utc),
            IPAddress = "127.0.0.2",
            UserAgent = "Deterministic Agent"
        };

        context.Users.AddRange(owner, accessor);
        context.DocumentTypes.Add(identityType);
        context.Documents.Add(document);
        context.Institutions.Add(institution);
        context.InstitutionEnquiryRequests.Add(request);
        context.DocumentAccessApprovals.Add(approval);
        context.DocumentAccessLogs.Add(log);
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var first = await controller.GetActivityReport(owner.Id);
        var second = await controller.GetActivityReport(owner.Id);

        var firstResult = Assert.IsType<OkObjectResult>(first.Result);
        var secondResult = Assert.IsType<OkObjectResult>(second.Result);

        var firstDto = Assert.IsType<ActivityReportDto>(firstResult.Value);
        var secondDto = Assert.IsType<ActivityReportDto>(secondResult.Value);

        var normalizedFirst = new
        {
            firstDto.DocumentOwner,
            firstDto.OwnerId,
            firstDto.ActiveDocuments,
            firstDto.InactiveDocuments,
            firstDto.TotalDocuments,
            firstDto.DistributionByCategory,
            firstDto.Inventory,
            firstDto.VaultAccessLog,
            firstDto.ClientRelationships
        };

        var normalizedSecond = new
        {
            secondDto.DocumentOwner,
            secondDto.OwnerId,
            secondDto.ActiveDocuments,
            secondDto.InactiveDocuments,
            secondDto.TotalDocuments,
            secondDto.DistributionByCategory,
            secondDto.Inventory,
            secondDto.VaultAccessLog,
            secondDto.ClientRelationships
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        Assert.Equal(
            JsonSerializer.Serialize(normalizedFirst, jsonOptions),
            JsonSerializer.Serialize(normalizedSecond, jsonOptions));

        Assert.Equal(1, firstDto.ActiveDocuments);
        Assert.Equal(0, firstDto.InactiveDocuments);
        Assert.Equal(1, firstDto.TotalDocuments);
        Assert.Equal("Det Owner", firstDto.DocumentOwner);
    }
}
