using System.Text;
using System.Text.Json;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Reports;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace FourierIT.API.Tests;

public class MonthlyReportTests
{
    [Fact]
    public async Task MonthlyReport_ReturnsDeterministicAggregatedData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonthlyReportTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" });
        context.Documents.AddRange(
            new Document
            {
                DocumentId = 1,
                FileName = "approved.pdf",
                DocumentTypeId = 1,
                CurrentStatus = "Approved",
                UploadedDate = new DateTime(2026, 7, 5, 8, 0, 0, DateTimeKind.Utc),
                ExpiryDate = new DateTimeOffset(2027, 7, 5, 0, 0, 0, TimeSpan.Zero),
                FileSizeBytes = 1024,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true
            },
            new Document
            {
                DocumentId = 2,
                FileName = "pending.pdf",
                DocumentTypeId = 1,
                CurrentStatus = "Pending",
                UploadedDate = new DateTime(2026, 7, 5, 9, 0, 0, DateTimeKind.Utc),
                ExpiryDate = new DateTimeOffset(2027, 7, 5, 0, 0, 0, TimeSpan.Zero),
                FileSizeBytes = 2048,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true
            });
        context.ComplianceStatuses.Add(new ComplianceStatus
        {
            ComplianceStatusId = 1,
            UserId = "user-1",
            OverallStatus = "Compliant",
            RiskLevel = "Low",
            CompliancePercentage = 90,
            LastChecked = new DateTime(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc)
        });
        context.AuditLogs.Add(new AuditLog
        {
            AuditLogId = 1,
            ActionCode = "LOGIN_FAILURE",
            TimeStamp = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero),
            TableAffected = "Users",
            Description = "Failed login"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var first = await controller.GetMonthlyReport(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        var second = await controller.GetMonthlyReport(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        var firstValue = Assert.IsType<OkObjectResult>(first.Result).Value;
        var secondValue = Assert.IsType<OkObjectResult>(second.Result).Value;
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var firstJson = JsonSerializer.Serialize(firstValue, jsonOptions);
        var secondJson = JsonSerializer.Serialize(secondValue, jsonOptions);
        using var firstDocument = JsonDocument.Parse(firstJson);
        using var secondDocument = JsonDocument.Parse(secondJson);

        Assert.Equal(
            firstDocument.RootElement.GetProperty("processing").GetRawText(),
            secondDocument.RootElement.GetProperty("processing").GetRawText());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("securityEvents").GetRawText(),
            secondDocument.RootElement.GetProperty("securityEvents").GetRawText());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("distribution").GetRawText(),
            secondDocument.RootElement.GetProperty("distribution").GetRawText());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("storage").GetRawText(),
            secondDocument.RootElement.GetProperty("storage").GetRawText());
        Assert.Equal(
            firstDocument.RootElement.GetProperty("uploadVolume").GetRawText(),
            secondDocument.RootElement.GetProperty("uploadVolume").GetRawText());
        Assert.Contains("July 2026", firstJson);
        Assert.Contains("\"totalUploads\":2", firstJson);
        Assert.Contains("\"failedLogins\":1", firstJson);
        Assert.Contains("\"verified\":1", firstJson);
        Assert.Contains("\"peakDay\":5", firstJson);
    }

    [Fact]
    public async Task ActivityReport_UsesDocumentTypeWarningDaysForStatus()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ActivityReportValidityTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);

        context.Users.Add(new User
        {
            Id = "user-activity-validity",
            UserName = "validity-user",
            Profile = new Profile
            {
                FirstName = "Validity",
                LastName = "User"
            }
        });

        var expiredType = new DocumentType { DocumentTypeId = 1, TypeName = "Expired Type", WarningDays = 30 };
        var warningType = new DocumentType { DocumentTypeId = 2, TypeName = "Warning Type", WarningDays = 30 };
        var outsideType = new DocumentType { DocumentTypeId = 3, TypeName = "Outside Type", WarningDays = 30 };
        context.DocumentTypes.AddRange(expiredType, warningType, outsideType);

        var now = DateTimeOffset.UtcNow;
        context.Documents.AddRange(
            new Document
            {
                DocumentId = 101,
                FileName = "expired.pdf",
                UserId = "user-activity-validity",
                DocumentTypeId = 1,
                CurrentStatus = "Verified",
                UploadedDate = now.AddDays(-90).UtcDateTime,
                ExpiryDate = now.AddDays(-1),
                FileSizeBytes = 1024,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true
            },
            new Document
            {
                DocumentId = 102,
                FileName = "warning.pdf",
                UserId = "user-activity-validity",
                DocumentTypeId = 2,
                CurrentStatus = "Verified",
                UploadedDate = now.AddDays(-90).UtcDateTime,
                ExpiryDate = now.AddDays(15),
                FileSizeBytes = 1024,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true
            },
            new Document
            {
                DocumentId = 103,
                FileName = "outside.pdf",
                UserId = "user-activity-validity",
                DocumentTypeId = 3,
                CurrentStatus = "Verified",
                UploadedDate = now.AddDays(-90).UtcDateTime,
                ExpiryDate = now.AddDays(45),
                FileSizeBytes = 1024,
                EncryptionAlgorithm = "AES-256",
                IsEncrypted = true
            });

        await context.SaveChangesAsync();

        var controller = new ReportsController(context, null, new DocumentValidityCalculator());
        var result = await controller.GetActivityReport("user-activity-validity");
        var report = Assert.IsType<ActivityReportDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("Expired", report.Inventory.Single(d => d.DocumentName == "expired.pdf").VerificationStatus);
        Assert.Equal("Expiring Soon", report.Inventory.Single(d => d.DocumentName == "warning.pdf").VerificationStatus);
        Assert.Equal("Verified", report.Inventory.Single(d => d.DocumentName == "outside.pdf").VerificationStatus);
    }

    [Fact]
    public async Task MonthlyReportPdf_ContainsValuesFromJsonEndpoint()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonthlyReportPdfTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" });
        context.Documents.Add(new Document
        {
            DocumentId = 31,
            FileName = "pdf-verified.pdf",
            DocumentTypeId = 1,
            CurrentStatus = "Verified",
            UploadedDate = new DateTime(2026, 7, 5, 8, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2027, 7, 5, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 1024,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context);
        var jsonResult = await controller.GetMonthlyReport(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        var jsonReport = Assert.IsType<MonthlyReportDto>(Assert.IsType<OkObjectResult>(jsonResult.Result).Value);
        var pdfResult = await controller.DownloadMonthlyPdf(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        var file = Assert.IsType<FileContentResult>(pdfResult);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(file.FileContents, 0, 5));
        using var pdf = PdfDocument.Open(file.FileContents);
        var text = string.Join("\n", pdf.GetPages().Select(page => page.Text));

        Assert.Contains(jsonReport.Month, text);
        Assert.Contains("Verified", text);
        Assert.Contains(jsonReport.Processing.Verified.ToString(), text);
        Assert.Contains("Total", text);
        Assert.Contains(jsonReport.TotalUploads.ToString(), text);
        Assert.Contains("Identity Document", text);
    }
}
