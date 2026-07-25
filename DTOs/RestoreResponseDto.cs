using System;

namespace FourierIT_API.DTOs
{
    public class RestoreResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset RestoredAt { get; set; }
    }
}
