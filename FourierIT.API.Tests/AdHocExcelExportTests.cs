using ClosedXML.Excel;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Reports;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FourierIT.API.Tests;

public class AdHocExcelExportTests
{
    [Fact]
    public async Task DownloadAdHocExcel_ReturnsParseableWorkbookWithReportHeaders()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdHocExcelExportTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" });
        context.Documents.Add(new Document
        {
            DocumentId = 7,
            FileName = "identity.pdf",
            DocumentTypeId = 1,
            CurrentStatus = "Uploaded",
            UploadedDate = new DateTime(2026, 2, 15, 9, 45, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2027, 2, 15, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 2048,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        });
        context.AdHocReports.Add(new AdHocReport
        {
            AdHocReportId = 42,
            Title = "Compliance Activity",
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 3, 31),
            FocusAreas = "[\"DOCUMENT_PROCESSING\"]",
            ExportFormat = "EXCEL",
            CreatedByUserId = "user-1",
            CreatedByName = "Test Admin",
            CreatedAt = new DateTime(2026, 8, 25, 10, 30, 0, DateTimeKind.Utc),
            Status = "ready"
        });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).DownloadAdHocExcel(42);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);

        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        var sheet = workbook.Worksheet("Document Activity");
        Assert.Equal("Compliance Activity", sheet.Cell("A1").GetString());
        Assert.Equal("Field", sheet.Cell("A3").GetString());
        Assert.Equal("Value", sheet.Cell("B3").GetString());
        Assert.Equal("Test Admin", sheet.Cell("B5").GetString());

        var dataSheet = workbook.Worksheet("Document Processing");
        Assert.Equal(2, dataSheet.LastRowUsed()!.RowNumber());
        Assert.Equal("Document ID", dataSheet.Cell("A1").GetString());
        Assert.Equal("identity.pdf", dataSheet.Cell("B2").GetString());
        Assert.Equal(XLDataType.DateTime, dataSheet.Cell("E2").DataType);
        Assert.Equal(XLDataType.Number, dataSheet.Cell("F2").DataType);
        Assert.Equal(2048, dataSheet.Cell("F2").GetDouble());
    }

    [Fact]
    public async Task DownloadAdHocExcel_UsesComplianceSourceForComplianceFocus()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdHocComplianceExcelTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.ComplianceStatuses.Add(new ComplianceStatus
        {
            ComplianceStatusId = 9,
            UserId = "user-9",
            OverallStatus = "Compliant",
            RiskLevel = "Low",
            CompliancePercentage = 92,
            OverallRiskScore = 8,
            LastChecked = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc)
        });
        context.AdHocReports.Add(new AdHocReport
        {
            AdHocReportId = 43,
            Title = "Compliance Status",
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 3, 31),
            FocusAreas = "[\"COMPLIANCE_STATUS\"]",
            ExportFormat = "EXCEL",
            CreatedByUserId = "user-1",
            CreatedByName = "Test Admin",
            CreatedAt = DateTime.UtcNow,
            Status = "ready"
        });
        await context.SaveChangesAsync();

        var result = await new ReportsController(context).DownloadAdHocExcel(43);
        var file = Assert.IsType<FileContentResult>(result);
        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        var dataSheet = workbook.Worksheet("Compliance Results");

        Assert.Equal(2, dataSheet.LastRowUsed()!.RowNumber());
        Assert.Equal("Compliance %", dataSheet.Cell("E1").GetString());
        Assert.Equal(XLDataType.Number, dataSheet.Cell("E2").DataType);
        Assert.Equal(92, dataSheet.Cell("E2").GetDouble());
        Assert.Equal(XLDataType.DateTime, dataSheet.Cell("G2").DataType);
        Assert.Throws<ArgumentException>(() => workbook.Worksheet("Document Processing"));
    }

    [Fact]
    public async Task GetAdHocReportData_ComplianceFocusReturnsComplianceDataWithoutDocumentData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdHocComplianceResultsTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 1, TypeName = "Identity Document" });
        context.Documents.Add(new Document
        {
            DocumentId = 99,
            FileName = "should-not-appear.pdf",
            DocumentTypeId = 1,
            CurrentStatus = "Verified",
            UploadedDate = new DateTime(2026, 2, 15, 9, 45, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTimeOffset(2027, 2, 15, 0, 0, 0, TimeSpan.Zero),
            FileSizeBytes = 2048,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        });
        context.ComplianceStatuses.Add(new ComplianceStatus
        {
            ComplianceStatusId = 19,
            UserId = "compliance-user",
            OverallStatus = "Non-Compliant",
            RiskLevel = "High",
            CompliancePercentage = 41,
            OverallRiskScore = 59,
            LastChecked = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim(ClaimTypes.GivenName, "Test Admin")
                    }, "test"))
                }
            }
        };

        var createResult = await controller.CreateAdHocReport(new AdHocReportRequestDto
        {
            Title = "Compliance Only",
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 3, 31),
            FocusAreas = new List<string> { "COMPLIANCE" },
            ExportFormat = "PDF"
        });
        Assert.IsType<OkObjectResult>(createResult.Result);

        var generatedReportId = await context.AdHocReports
            .Select(report => report.AdHocReportId)
            .SingleAsync();
        var result = await controller.GetAdHocReportData(generatedReportId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var data = Assert.IsType<AdHocReportDataDto>(ok.Value);
        Assert.Single(data.ComplianceResults);
        Assert.Equal(41, data.ComplianceResults[0].CompliancePercentage);
        Assert.Empty(data.DocumentResults);
        Assert.Empty(data.DistributionResults);
        Assert.Null(data.StorageResult);
        Assert.DoesNotContain(data.DocumentResults, item => item.FileName == "should-not-appear.pdf");
    }
}
