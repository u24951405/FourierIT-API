using FourierIT_API.DTOs.Notification;

namespace FourierIT_API.Interfaces
{
    public interface IInAppNotificationService
    {
        Task NotifyUsersAsync(IEnumerable<string> userIds, string subject, string message, string? category = null, int? documentId = null);
        Task<List<UserNotificationDto>> GetForUserAsync(string userId, int take = 20);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> MarkAsReadAsync(string userId, int notificationId);
        Task<int> MarkAllAsReadAsync(string userId);
    }
}
