namespace FourierIT_API.DTOs.Notification
{
    public class UserNotificationDto
    {
        public int NotificationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int? DocumentId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
