using FourierIT_API.Data;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.EntityFrameworkCore;

namespace FourierIT.API.Tests;

public class InAppNotificationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InAppNotificationServiceTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Users.AddRange(
            new User { Id = "owner", UserName = "owner@test.local" },
            new User { Id = "admin", UserName = "admin@test.local" });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task NotifyUsers_DeliversOneUnreadEntryPerDistinctRecipient()
    {
        await using var context = CreateContext();
        var service = new InAppNotificationService(context);

        await service.NotifyUsersAsync(new[] { "owner", "admin", "owner", "" }, "Document rejected", "Reason: illegible", "DocumentRejected", 42);

        Assert.Equal(1, await context.Notifications.CountAsync());
        Assert.Equal(1, await service.GetUnreadCountAsync("owner"));
        Assert.Equal(1, await service.GetUnreadCountAsync("admin"));

        var ownerNotification = Assert.Single(await service.GetForUserAsync("owner"));
        Assert.Equal("DocumentRejected", ownerNotification.Category);
        Assert.Equal(42, ownerNotification.DocumentId);
        Assert.False(ownerNotification.IsRead);
    }

    [Fact]
    public async Task MarkAsRead_OnlyAffectsTheCallingUser()
    {
        await using var context = CreateContext();
        var service = new InAppNotificationService(context);
        await service.NotifyUsersAsync(new[] { "owner", "admin" }, "Document approved", "Approved");
        var notificationId = (await service.GetForUserAsync("owner")).Single().NotificationId;

        Assert.True(await service.MarkAsReadAsync("owner", notificationId));

        Assert.Equal(0, await service.GetUnreadCountAsync("owner"));
        Assert.Equal(1, await service.GetUnreadCountAsync("admin"));
        Assert.False(await service.MarkAsReadAsync("owner", notificationId + 999));
    }

    [Fact]
    public async Task MarkAllAsRead_ClearsUnreadCount()
    {
        await using var context = CreateContext();
        var service = new InAppNotificationService(context);
        await service.NotifyUsersAsync(new[] { "owner" }, "One", "First");
        await service.NotifyUsersAsync(new[] { "owner" }, "Two", "Second");

        Assert.Equal(2, await service.MarkAllAsReadAsync("owner"));
        Assert.Equal(0, await service.GetUnreadCountAsync("owner"));
    }
}
