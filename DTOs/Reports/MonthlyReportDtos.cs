namespace FourierIT_API.DTOs.Reports;

public sealed class MonthlyReportDto
{
    public string ReportId { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public DateTime DateGenerated { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public MonthlyProcessingStatsDto Processing { get; set; } = new();
    public List<MonthlySecurityEventDto> SecurityEvents { get; set; } = new();
    public List<MonthlyDistributionCategoryDto> Distribution { get; set; } = new();
    public MonthlyStorageStatsDto Storage { get; set; } = new();
    public List<MonthlyUploadVolumeDto> UploadVolume { get; set; } = new();
    public int TotalUploads { get; set; }
    public decimal DailyAverage { get; set; }
    public int PeakDay { get; set; }
}

public sealed class MonthlyProcessingStatsDto
{
    public int Verified { get; set; }
    public int PendingVerification { get; set; }
    public int FlaggedAnomalies { get; set; }
    public int PartOfEnquiry { get; set; }
}

public sealed class MonthlySecurityEventDto
{
    public int Day { get; set; }
    public int FailedLogins { get; set; }
    public int UnusualAccessPattern { get; set; }
    public int PermissionElevationRequest { get; set; }
}

public sealed class MonthlyDistributionCategoryDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class MonthlyStorageStatsDto
{
    public decimal UsedGb { get; set; }
    public decimal AvailableGb { get; set; }
    public decimal TotalGb { get; set; }
    public decimal UsedPercentage { get; set; }
}

public sealed class MonthlyUploadVolumeDto
{
    public int Day { get; set; }
    public int Count { get; set; }
}
