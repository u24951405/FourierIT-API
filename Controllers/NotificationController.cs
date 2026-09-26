using FourierIT_API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FourierIT_API.Controllers
{
    /// <summary>
    /// In-app notifications for the signed-in user.
    /// </summary>
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly IInAppNotificationService _notifications;

        public NotificationController(IInAppNotificationService notifications)
        {
            _notifications = notifications;
        }

        [HttpGet]
        public async Task<IActionResult> GetMine([FromQuery] int take = 20)
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();

            return Ok(await _notifications.GetForUserAsync(userId, take));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();

            return Ok(new { count = await _notifications.GetUnreadCountAsync(userId) });
        }

        [HttpPost("{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();

            return await _notifications.MarkAsReadAsync(userId, notificationId) ? NoContent() : NotFound();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = CurrentUserId();
            if (userId == null) return Unauthorized();

            return Ok(new { updated = await _notifications.MarkAllAsReadAsync(userId) });
        }

        private string? CurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
