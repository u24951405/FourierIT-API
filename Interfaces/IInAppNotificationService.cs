using FourierIT_API.DTOs.Notification;

namespace FourierIT_API.Interfaces
{
    public interface IInAppNotificationService
    {
        /// <summary>
        /// Notifies users in the app. link is the app page the notification opens (e.g. "/documents/requests").
        /// With sendEmail, each recipient who has an email address is also emailed (in the background).
        /// </summary>
        Task NotifyUsersAsync(IEnumerable<string> userIds, string subject, string message, string? category = null,
            int? documentId = null, string? link = null, bool sendEmail = false);

        /// <summary>Emails someone outside the app, such as an institution contact. Queued; never throws.</summary>
        void EmailExternal(string? toEmail, string subject, string message, string? actionUrl = null, string? actionLabel = null);

        /// <summary>User IDs of everyone holding a role (optionally only those in one department).</summary>
        Task<List<string>> GetUserIdsInRoleAsync(string roleName, int? departmentId = null);
        Task<List<UserNotificationDto>> GetForUserAsync(string userId, int take = 20);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> MarkAsReadAsync(string userId, int notificationId);
        Task<int> MarkAllAsReadAsync(string userId);
    }
}
