using FourierIT_API.Data;
using FourierIT_API.DTOs.Notification;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Services
{
    /// <summary>
    /// Stores notifications shown in the app (notification bell). One Notification row is shared by all recipients;
    /// each recipient gets a UserNotification row that tracks whether they have read it.
    /// </summary>
    public class InAppNotificationService : IInAppNotificationService
    {
        private const int SubjectMaxLength = 200;
        private const int MessageMaxLength = 500;

        private readonly AppDbContext _context;

        public InAppNotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task NotifyUsersAsync(IEnumerable<string> userIds, string subject, string message, string? category = null, int? documentId = null)
        {
            var recipients = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (recipients.Count == 0) return;

            var notification = new Notification
            {
                Subject = Truncate(subject, SubjectMaxLength),
                Message = Truncate(message, MessageMaxLength),
                Category = category,
                DocumentId = documentId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            foreach (var userId in recipients)
            {
                notification.UserNotifications.Add(new UserNotification { UserId = userId, IsRead = false });
            }

            notification.NotificationHistories.Add(new NotificationHistory
            {
                DeliveryMethod = "InApp",
                SentAt = notification.CreatedAt
            });

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<UserNotificationDto>> GetForUserAsync(string userId, int take = 20)
        {
            take = Math.Clamp(take, 1, 100);

            return await _context.UserNotifications
                .AsNoTracking()
                .Where(un => un.UserId == userId)
                .OrderByDescending(un => un.Notification.CreatedAt)
                .ThenByDescending(un => un.NotificationId)
                .Take(take)
                .Select(un => new UserNotificationDto
                {
                    NotificationId = un.NotificationId,
                    Subject = un.Notification.Subject,
                    Message = un.Notification.Message,
                    Category = un.Notification.Category,
                    DocumentId = un.Notification.DocumentId,
                    CreatedAt = un.Notification.CreatedAt,
                    IsRead = un.IsRead
                })
                .ToListAsync();
        }

        public Task<int> GetUnreadCountAsync(string userId)
        {
            return _context.UserNotifications.CountAsync(un => un.UserId == userId && !un.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(string userId, int notificationId)
        {
            var entry = await _context.UserNotifications
                .FirstOrDefaultAsync(un => un.UserId == userId && un.NotificationId == notificationId);

            if (entry == null) return false;
            if (entry.IsRead) return true;

            entry.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.UserNotifications
                .Where(un => un.UserId == userId && !un.IsRead)
                .ToListAsync();

            foreach (var entry in unread)
                entry.IsRead = true;

            await _context.SaveChangesAsync();
            return unread.Count;
        }

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }
}
