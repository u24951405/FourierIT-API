using FourierIT_API.Data;
using FourierIT_API.Interfaces;
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

    private sealed class RecordingEmailQueue : INotificationEmailQueue
    {
        public List<NotificationEmail> Sent { get; } = new();
        public bool Enqueue(NotificationEmail email) { Sent.Add(email); return true; }
    }

    [Fact]
    public async Task NotifyUsers_WithEmail_QueuesOneEmailPerRecipientWithAnAddressAndStoresTheLink()
    {
        await using var context = CreateContext();
        context.Users.Add(new User { Id = "officer", UserName = "officer", Email = "officer@test.local" });
        (await context.Users.FindAsync("owner"))!.Email = "owner@test.local";
        await context.SaveChangesAsync();
        var queue = new RecordingEmailQueue();
        var service = new InAppNotificationService(context, queue);

        // "admin" has no email address, so they only get the in-app notification.
        await service.NotifyUsersAsync(new[] { "owner", "officer", "admin" }, "Document waiting for review", "Please review",
            "ReviewRequested", link: "/compliance/review-queue", sendEmail: true);

        Assert.Equal(new[] { "officer@test.local", "owner@test.local" }, queue.Sent.Select(e => e.ToEmail).OrderBy(e => e));
        Assert.All(queue.Sent, email => Assert.Equal("/compliance/review-queue", email.ActionPath));
        Assert.Equal("/compliance/review-queue", Assert.Single(await service.GetForUserAsync("admin")).Link);
    }

    [Fact]
    public async Task NotifyUsers_WithoutEmail_QueuesNothing()
    {
        await using var context = CreateContext();
        (await context.Users.FindAsync("owner"))!.Email = "owner@test.local";
        await context.SaveChangesAsync();
        var queue = new RecordingEmailQueue();
        var service = new InAppNotificationService(context, queue);

        await service.NotifyUsersAsync(new[] { "owner" }, "Request cancelled", "No action needed");

        Assert.Empty(queue.Sent);
        Assert.Equal(1, await service.GetUnreadCountAsync("owner"));
    }

    [Fact]
    public async Task EmailExternal_SkipsMissingAddresses()
    {
        await using var context = CreateContext();
        var queue = new RecordingEmailQueue();
        var service = new InAppNotificationService(context, queue);

        service.EmailExternal(null, "Request approved", "Approved");
        service.EmailExternal("  contact@bank.test ", "Request approved", "Approved");

        Assert.Equal("contact@bank.test", Assert.Single(queue.Sent).ToEmail);
    }

    [Fact]
    public async Task NotifyUsers_WithEmail_SkipsUsersWhoTurnedEmailsOff()
    {
        await using var context = CreateContext();
        var owner = (await context.Users.FindAsync("owner"))!;
        owner.Email = "owner@test.local";
        owner.EmailNotificationsEnabled = false;
        var admin = (await context.Users.FindAsync("admin"))!;
        admin.Email = "admin@test.local";
        await context.SaveChangesAsync();
        var queue = new RecordingEmailQueue();
        var service = new InAppNotificationService(context, queue);

        await service.NotifyUsersAsync(new[] { "owner", "admin" }, "Document approved", "Approved", sendEmail: true);

        Assert.Equal("admin@test.local", Assert.Single(queue.Sent).ToEmail);
        // Turning emails off never hides the in-app notification.
        Assert.Equal(1, await service.GetUnreadCountAsync("owner"));
    }
}
