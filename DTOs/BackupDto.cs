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
