using System;

namespace FourierIT_API.DTOs
{
    /// <summary>
    /// Data Transfer Object representing an audit log record.
    /// </summary>
    public class AuditLogDto
    {
        public int AuditLogId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string ActionCode { get; set; } = string.Empty;

        public DateTimeOffset TimeStamp { get; set; }

        public string? Description { get; set; }

        public string TableAffected { get; set; } = string.Empty;

        public int? RecordID { get; set; }

        public string? PreviousBlockHash { get; set; }

        public string? BlockHash { get; set; }
    }
}
