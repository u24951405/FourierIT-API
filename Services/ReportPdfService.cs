using System.Globalization;
using System.Security;
using System.Text;
using FourierIT_API.DTOs.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FourierIT_API.Services;

public sealed class ReportPdfService
{
    public ReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateMonthly(MonthlyReportDto report) => BuildDocument(
        report.Month, report.ReportId, report.DateGenerated, report.CreatedBy, column =>
        {
            AddTable(column, "Processing", new[] { "Metric", "Value" }, new[]
            {
                new[] { "Verified", report.Processing.Verified.ToString() },
                new[] { "Pending verification", report.Processing.PendingVerification.ToString() },
                new[] { "Flagged anomalies", report.Processing.FlaggedAnomalies.ToString() },
                new[] { "Part of enquiry", report.Processing.PartOfEnquiry.ToString() }
            });
            AddChart(column, "Processing outcomes", new[]
            {
                ("Verified", report.Processing.Verified),
                ("Pending", report.Processing.PendingVerification),
                ("Flagged", report.Processing.FlaggedAnomalies),
                ("Enquiry", report.Processing.PartOfEnquiry)
            });
            AddTable(column, "Security events", new[] { "Day", "Failed logins", "Unusual access", "Permission requests" },
                report.SecurityEvents.Select(item => new[] { item.Day.ToString(), item.FailedLogins.ToString(), item.UnusualAccessPattern.ToString(), item.PermissionElevationRequest.ToString() }).ToArray());
            AddTable(column, "Distribution", new[] { "Category", "Count", "Percentage" },
                report.Distribution.Select(item => new[] { item.Label, item.Count.ToString(), $"{item.Percentage.ToString(CultureInfo.InvariantCulture)}%" }).ToArray());
            AddTable(column, "Storage", new[] { "Metric", "Value" }, new[]
            {
                new[] { "Used (GB)", report.Storage.UsedGb.ToString(CultureInfo.InvariantCulture) },
                new[] { "Available (GB)", report.Storage.AvailableGb.ToString(CultureInfo.InvariantCulture) },
                new[] { "Total (GB)", report.Storage.TotalGb.ToString(CultureInfo.InvariantCulture) },
                new[] { "Used percentage", $"{report.Storage.UsedPercentage.ToString(CultureInfo.InvariantCulture)}%" }
            });
            AddTable(column, "Upload volume", new[] { "Day", "Uploads" },
                report.UploadVolume.Select(item => new[] { item.Day.ToString(), item.Count.ToString() }).ToArray());
        });

    public byte[] GenerateActivity(ActivityReportDto report) => BuildDocument(
        "Document Activity Report", report.ReportId, report.DateGenerated, report.DocumentOwner, column =>
        {
            AddTable(column, "Document overview", new[] { "Metric", "Value" }, new[]
            {
                new[] { "Active documents", report.ActiveDocuments.ToString() },
                new[] { "Inactive documents", report.InactiveDocuments.ToString() },
                new[] { "Total documents", report.TotalDocuments.ToString() }
            });
            AddChart(column, "Documents by category", report.DistributionByCategory.Select(item => (item.Label, item.Count)).ToArray());
            AddTable(column, "Document inventory", new[] { "Document", "Category", "Uploaded", "Expiry", "Status" },
                report.Inventory.Select(item => new[] { item.DocumentName, item.Category, item.UploadDate, item.ExpiryDate ?? "-", item.VerificationStatus }).ToArray());
            AddTable(column, "Vault access log", new[] { "Timestamp", "Accessor", "Role", "Action", "Organisation" },
                report.VaultAccessLog.Select(item => new[] { item.Timestamp, item.AccessorName, item.AccessorRole, item.ActionReason, item.Organisation }).ToArray());
            AddTable(column, "Client relationships", new[] { "Organisation", "Documents shared", "Status" },
                report.ClientRelationships.Select(item => new[] { item.Organisation, item.DocumentsShared.ToString(), item.Status }).ToArray());
        });

    public byte[] GenerateAdHoc(AdHocReportDataDto report) => BuildDocument(
        report.Title, $"AD-HOC-{report.ReportId}", report.DateGenerated, report.CreatedBy, column =>
        {
            AddTable(column, "Saved criteria", new[] { "Field", "Value" }, new[]
            {
                new[] { "Date from", report.DateFrom.ToString("yyyy-MM-dd") },
                new[] { "Date to", report.DateTo.ToString("yyyy-MM-dd") },
                new[] { "Focus areas", string.Join(", ", report.FocusAreas) }
            });
            if (report.ComplianceResults.Count > 0)
                AddTable(column, "Compliance results", new[] { "User", "Status", "Risk", "Compliance %", "Last checked" },
                    report.ComplianceResults.Select(item => new[] { item.UserId, item.OverallStatus, item.RiskLevel, $"{item.CompliancePercentage}%", item.LastChecked.ToString("yyyy-MM-dd HH:mm") }).ToArray());
            if (report.DocumentResults.Count > 0)
                AddTable(column, "Document processing", new[] { "File", "Type", "Status", "Uploaded", "Size" },
                    report.DocumentResults.Select(item => new[] { item.FileName, item.DocumentType, item.Status, item.UploadedDate.ToString("yyyy-MM-dd"), item.FileSizeBytes.ToString() }).ToArray());
            if (report.DistributionResults.Count > 0)
                AddTable(column, "Document distribution", new[] { "Type", "Count", "Size" },
                    report.DistributionResults.Select(item => new[] { item.DocumentType, item.DocumentCount.ToString(), item.TotalSizeBytes.ToString() }).ToArray());
            if (report.SecurityResults.Count > 0)
                AddTable(column, "Security anomalies", new[] { "Action", "User", "Timestamp", "Description" },
                    report.SecurityResults.Select(item => new[] { item.Action, item.UserId, item.Timestamp.ToString("yyyy-MM-dd HH:mm"), item.Description }).ToArray());
            if (report.StorageResult != null)
                AddTable(column, "System storage", new[] { "From", "To", "Documents", "Size" }, new[]
                {
                    new[] { report.StorageResult.DateFrom.ToString("yyyy-MM-dd"), report.StorageResult.DateTo.ToString("yyyy-MM-dd"), report.StorageResult.DocumentCount.ToString(), report.StorageResult.TotalSizeBytes.ToString() }
                });
            if (report.UploadVolumeResults.Count > 0)
                AddTable(column, "Upload volume", new[] { "Date", "Uploads" },
                    report.UploadVolumeResults.Select(item => new[] { item.UploadDate.ToString("yyyy-MM-dd"), item.UploadCount.ToString() }).ToArray());
        });

    private static byte[] BuildDocument(string title, string reportId, DateTime generated, string createdBy, Action<ColumnDescriptor> content)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily(Fonts.Arial).FontSize(9));
            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(brand =>
                    {
                        brand.Item().Text("FOURIER GROUP").FontSize(18).Bold().FontColor("0F766E");
                        brand.Item().Text("VERIFICATION & COMPLIANCE PLATFORM").FontSize(8).FontColor("64748B");
                    });
                    row.ConstantItem(180).Column(meta =>
                    {
                        meta.Item().AlignRight().Text(title).Bold();
                        meta.Item().AlignRight().Text($"Report ID: {reportId}").FontSize(8);
                    });
                });
                header.Item().PaddingTop(8).LineHorizontal(1).LineColor("0F766E");
                header.Item().PaddingTop(5).Text($"Generated {generated:yyyy-MM-dd HH:mm:ss 'UTC'} by {createdBy}").FontSize(8).FontColor("64748B");
            });
            page.Content().PaddingTop(12).Column(content);
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("DocuVault structured report | Page ");
                text.CurrentPageNumber();
            });
        }));
        return document.GeneratePdf();
    }

    private static void AddTable(ColumnDescriptor column, string title, string[] headers, string[][] rows)
    {
        column.Item().PaddingTop(12).Text(title).FontSize(13).Bold().FontColor("0F766E");
        column.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var index = 0; index < headers.Length; index++) columns.RelativeColumn();
            });
            foreach (var header in headers)
                table.Cell().Background("0F766E").Padding(5).Text(header).FontColor(Colors.White).Bold();
            foreach (var row in rows)
                foreach (var value in row)
                    table.Cell().BorderBottom(1).BorderColor("E2E8F0").Padding(5).Text(value ?? "-");
        });
    }

    private static void AddChart(ColumnDescriptor column, string title, (string Label, int Value)[] values)
    {
        if (values.Length == 0) return;
        column.Item().PaddingTop(12).Text(title).FontSize(13).Bold().FontColor("0F766E");
        column.Item().PaddingTop(4).AspectRatio(4.6f).Svg(BuildChartSvg(values));
    }

    private static string BuildChartSvg((string Label, int Value)[] values)
    {
        const int width = 700;
        const int height = 150;
        var max = Math.Max(1, values.Max(item => item.Value));
        var builder = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'><rect width='100%' height='100%' fill='#F8FAFC'/>");
        var slot = (width - 40) / values.Length;
        var barWidth = Math.Max(20, slot - 10);
        for (var index = 0; index < values.Length; index++)
        {
            var x = 20 + index * slot;
            var barHeight = values[index].Value * 95 / max;
            var y = 115 - barHeight;
            builder.Append($"<rect x='{x}' y='{y}' width='{barWidth}' height='{barHeight}' rx='3' fill='#0F766E'/>");
            builder.Append($"<text x='{x + barWidth / 2}' y='132' text-anchor='middle' font-family='Arial' font-size='10' fill='#334155'>{Escape(values[index].Label)}</text>");
            builder.Append($"<text x='{x + barWidth / 2}' y='{Math.Max(12, y - 4)}' text-anchor='middle' font-family='Arial' font-size='10' fill='#0F766E'>{values[index].Value}</text>");
        }
        return builder.Append("</svg>").ToString();
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
