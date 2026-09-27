using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.DocumentType;
using FourierIT_API.Interfaces;
using FourierIT_API.DTOs;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentTypeValidityPolicyTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PreviewValidity_ReturnsCorrectCounts_WithoutWriting()
    {
        await using var context = CreateContext();
        var documentType = new DocumentType
        {
            DocumentTypeId = 1,
            TypeName = "Test Type",
            Description = "Test description",
            ValidityMonths = 12,
            WarningDays = 30,
            ValidityBasis = ValidityBasis.CertificationDate,
        };
        context.DocumentTypes.Add(documentType);

        var user = new User { Id = "u1", UserName = "tester" };
        context.Users.Add(user);
        context.Documents.AddRange(
            new Document
            {
                DocumentId = 1,
                FileName = "expired.pdf",
                UserId = "u1",
                DocumentTypeId = 1,
                CurrentStatus = "Expired",
                UploadedDate = DateTime.UtcNow.AddDays(-100),
                ExpiryDate = DateTimeOffset.UtcNow.AddDays(-1),
                FileSizeBytes = 10,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true,
                CertificationDetails = new List<CertificationDetails>
                {
                    new() { CertificationID = "cert-1", CertificationDate = DateTimeOffset.UtcNow.AddDays(-5) }
                }
            },
            new Document
            {
                DocumentId = 2,
                FileName = "warning.pdf",
                UserId = "u1",
                DocumentTypeId = 1,
                CurrentStatus = "Verified",
                UploadedDate = DateTime.UtcNow.AddDays(-60),
                ExpiryDate = DateTimeOffset.UtcNow.AddDays(15),
                FileSizeBytes = 10,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true,
                CertificationDetails = new List<CertificationDetails>
                {
                    new() { CertificationID = "cert-2", CertificationDate = DateTimeOffset.UtcNow.AddDays(-10) }
                }
            },
            new Document
            {
                DocumentId = 3,
                FileName = "outside.pdf",
                UserId = "u1",
                DocumentTypeId = 1,
                CurrentStatus = "Verified",
                UploadedDate = DateTime.UtcNow.AddDays(-60),
                ExpiryDate = DateTimeOffset.UtcNow.AddDays(45),
                FileSizeBytes = 10,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true,
                CertificationDetails = new List<CertificationDetails>
                {
                    new() { CertificationID = "cert-3", CertificationDate = DateTimeOffset.UtcNow.AddDays(-10) }
                }
            },
            new Document
            {
                DocumentId = 4,
                FileName = "never.pdf",
                UserId = "u1",
                DocumentTypeId = 1,
                CurrentStatus = "Verified",
                UploadedDate = DateTime.UtcNow.AddDays(-60),
                ExpiryDate = DateTimeOffset.MaxValue,
                FileSizeBytes = 10,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true,
                CertificationDetails = new List<CertificationDetails>
                {
                    new() { CertificationID = "cert-4", CertificationDate = DateTimeOffset.UtcNow.AddDays(-10) }
                }
            });
        await context.SaveChangesAsync();

        var complianceService = new Mock<IComplianceService>();
        var auditLogService = new Mock<IAuditLogService>();
        auditLogService.Setup(x => x.CreateAuditLogAsync(It.IsAny<AuditLog>())).ReturnsAsync(new AuditLogDto());

        var service = new DocumentValidityPolicyService(
            context,
            new DocumentValidityCalculator(),
            complianceService.Object,
            auditLogService.Object,
            new LoggerFactory().CreateLogger<DocumentValidityPolicyService>());

        var controller = new DocumentTypesController(context, service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("superadmin", "true") }, "TestAuth"))
            }
        };

        var request = new DocumentTypeValidityUpdateRequest
        {
            ValidityMonths = 12,
            ValidityBasis = ValidityBasis.CertificationDate,
            WarningDays = 30
        };

        var result = await controller.PreviewValidity(1, request);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<DocumentTypeValiditySummaryDto>(ok.Value);

        Assert.Equal(4, summary.AffectedDocuments);
        Assert.Equal(0, summary.BecomeExpired);
        Assert.Equal(1, summary.NoLongerExpired);
        Assert.Equal(0, summary.MissingSourceDate);

        var documentTypeAfter = await context.DocumentTypes.AsNoTracking().SingleAsync(dt => dt.DocumentTypeId == 1);
        Assert.Equal(12, documentTypeAfter.ValidityMonths);
        Assert.Equal(30, documentTypeAfter.WarningDays);
    }

    [Fact]
    public async Task SaveValidity_CalculatesExpiry_AndWritesAuditLog()
    {
        await using var context = CreateContext();
        var documentType = new DocumentType
        {
            DocumentTypeId = 1,
            TypeName = "Type A",
            Description = "desc",
            ValidityMonths = 12,
            WarningDays = 30,
            ValidityBasis = ValidityBasis.UploadDate,
        };
        var user = new User { Id = "u1", UserName = "user1" };
        context.Users.Add(user);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(new Document
        {
            DocumentId = 1,
            FileName = "doc.pdf",
            UserId = "u1",
            DocumentTypeId = 1,
            CurrentStatus = "Verified",
            UploadedDate = DateTime.UtcNow.AddDays(-100),
            ExpiryDate = DateTimeOffset.UtcNow.AddDays(10),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            CertificationDetails = new List<CertificationDetails>()
        });
        await context.SaveChangesAsync();

        var complianceMock = new Mock<IComplianceService>();
        complianceMock.Setup(x => x.CheckUserComplianceAsync("u1", true)).ReturnsAsync(new ComplianceStatus { UserId = "u1" });
        var auditLogService = new Mock<IAuditLogService>();
        auditLogService.Setup(x => x.CreateAuditLogAsync(It.IsAny<AuditLog>())).ReturnsAsync(new AuditLogDto());

        var service = new DocumentValidityPolicyService(
            context,
            new DocumentValidityCalculator(),
            complianceMock.Object,
            auditLogService.Object,
            new LoggerFactory().CreateLogger<DocumentValidityPolicyService>());

        var controller = new DocumentTypesController(context, service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("superadmin", "true") }, "TestAuth"))
            }
        };

        var request = new DocumentTypeValidityUpdateRequest
        {
            ValidityMonths = 6,
            ValidityBasis = ValidityBasis.UploadDate,
            WarningDays = 15
        };

        var result = await controller.UpdateValidity(1, request);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<DocumentTypeValiditySummaryDto>(ok.Value);

        Assert.Equal(1, summary.AffectedDocuments);
        Assert.True(summary.ComplianceRecalculationFailed == false);

        var savedDocument = await context.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == 1);
        Assert.NotEqual(DateTimeOffset.UtcNow.AddDays(10), savedDocument.ExpiryDate);
        Assert.Equal("Verified", savedDocument.CurrentStatus);

        var logs = await context.AuditLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("DOCUMENT_TYPE_VALIDITY_UPDATED", logs[0].ActionCode);
    }

    [Fact]
    public async Task SaveValidity_RestoresPreviousNonExpiredStatus_WhenDocumentWasPending()
    {
        await using var context = CreateContext();
        var documentType = new DocumentType
        {
            DocumentTypeId = 1,
            TypeName = "Type A",
            Description = "desc",
            ValidityMonths = 12,
            WarningDays = 30,
            ValidityBasis = ValidityBasis.UploadDate,
        };
        var user = new User { Id = "u1", UserName = "user1" };
        context.Users.Add(user);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(new Document
        {
            DocumentId = 1,
            FileName = "doc.pdf",
            UserId = "u1",
            DocumentTypeId = 1,
            CurrentStatus = "Pending",
            UploadedDate = DateTime.UtcNow.AddDays(-100),
            ExpiryDate = DateTimeOffset.UtcNow.AddDays(-1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true,
            CertificationDetails = new List<CertificationDetails>(),
            DocumentStatusHistories = new List<DocumentStatusHistory>
            {
                new() { StatusName = "Pending", DateArchived = DateTimeOffset.UtcNow.AddDays(-50) },
                new() { StatusName = "Expired", DateArchived = DateTimeOffset.UtcNow.AddDays(-10) }
            }
        });
        await context.SaveChangesAsync();

        var complianceMock = new Mock<IComplianceService>();
        complianceMock.Setup(x => x.CheckUserComplianceAsync("u1", true)).ReturnsAsync(new ComplianceStatus { UserId = "u1" });
        var auditLogService = new Mock<IAuditLogService>();
        auditLogService.Setup(x => x.CreateAuditLogAsync(It.IsAny<AuditLog>())).ReturnsAsync(new AuditLogDto());

        var service = new DocumentValidityPolicyService(
            context,
            new DocumentValidityCalculator(),
            complianceMock.Object,
            auditLogService.Object,
            new LoggerFactory().CreateLogger<DocumentValidityPolicyService>());

        var controller = new DocumentTypesController(context, service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("superadmin", "true") }, "TestAuth"))
            }
        };

        var request = new DocumentTypeValidityUpdateRequest
        {
            ValidityMonths = 6,
            ValidityBasis = ValidityBasis.UploadDate,
            WarningDays = 15
        };

        var result = await controller.UpdateValidity(1, request);
        Assert.IsType<OkObjectResult>(result.Result);

        var savedDocument = await context.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == 1);
        Assert.Equal("Pending", savedDocument.CurrentStatus);
        Assert.NotEqual("Verified", savedDocument.CurrentStatus);
    }

    [Theory]
    [InlineData(0, ValidityBasis.UploadDate, 10)]
    [InlineData(121, ValidityBasis.UploadDate, 10)]
    [InlineData(12, ValidityBasis.UploadDate, 365)]
    [InlineData(12, (ValidityBasis)999, 10)]
    [InlineData(12, ValidityBasis.UploadDate, 360)]
    public async Task ValidationRules_Return400(int validityMonths, ValidityBasis basis, int warningDays)
    {
        await using var context = CreateContext();
        var documentType = new DocumentType { DocumentTypeId = 1, TypeName = "Type", Description = "desc" };
        context.DocumentTypes.Add(documentType);
        await context.SaveChangesAsync();

        var controller = new DocumentTypesController(context, new DocumentValidityPolicyService(
            context,
            new DocumentValidityCalculator(),
            new Mock<IComplianceService>().Object,
            new Mock<IAuditLogService>().Object,
            new LoggerFactory().CreateLogger<DocumentValidityPolicyService>()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("superadmin", "true") }, "TestAuth"))
            }
        };

        var request = new DocumentTypeValidityUpdateRequest
        {
            ValidityMonths = validityMonths,
            ValidityBasis = basis,
            WarningDays = warningDays
        };

        var result = await controller.UpdateValidity(1, request);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task NonSuperAdmin_IsRejected()
    {
        await using var context = CreateContext();
        var controller = new DocumentTypesController(context, new DocumentValidityPolicyService(
            context,
            new DocumentValidityCalculator(),
            new Mock<IComplianceService>().Object,
            new Mock<IAuditLogService>().Object,
            new LoggerFactory().CreateLogger<DocumentValidityPolicyService>()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "user") }, "TestAuth"))
            }
        };

        var result = await controller.PreviewValidity(1, new DocumentTypeValidityUpdateRequest
        {
            ValidityMonths = 12,
            ValidityBasis = ValidityBasis.UploadDate,
            WarningDays = 10
        });

        Assert.IsType<ForbidResult>(result.Result);
    }
}
