using System;
using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs
{
    public class CreateBackupRequestDto
    {
        [Required]
        public string? UserId { get; set; } = string.Empty;

        public bool IsManualBackup { get; set; } = false;
    }

    public class BackupResponseDto
    {
        /// <summary>True when the backup was made and uploaded; StatusMessage says why when it wasn't.</summary>
        public bool Success { get; set; }

        /// <summary>How long SQL Server took to write the backup, and the upload to Azure took (seconds).</summary>
        public double BackupSeconds { get; set; }
        public double UploadSeconds { get; set; }
        public long SizeBytes { get; set; }

        public int BackupId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        // Azure Blob URL
        public string FilePath { get; set; } = string.Empty;

        public DateTimeOffset DateBackedUp { get; set; }

        public bool IsManualBackup { get; set; }

        public string StatusMessage { get; set; } = string.Empty;
    }
}
