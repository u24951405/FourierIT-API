using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// Several tables reference AspNetUsers with a NO ACTION (or Restrict) delete rule, so SQL
/// Server rejects deleting a user while any row still points at them - e.g. an AuditLog entry
/// from something they once did, or a ComplianceAlert they acknowledged for someone else.
/// DeleteUserRelatedRecordsAsync must null out (or remove) every such reference before the user
/// row is deleted. Uses a real Sqlite database with foreign keys enforced - the EF InMemory
/// provider does not enforce FK constraints and would let this pass even if broken.
/// </summary>
public class SuperAdminUserDeletionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public SuperAdminUserDeletionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task DeleteUserById_BySuperAdmin_WithAuditLogAndCrossUserComplianceRefs_DeletesWithoutFkViolation()
    {
        var superAdmin = new User { Id = "super-admin-1", UserName = "superadmin", Email = "superadmin@test.local" };
        var otherUser = new User { Id = "other-user", UserName = "other@test.local", Email = "other@test.local" };
        var targetUser = new User { Id = "target-user", UserName = "target@test.local", Email = "target@test.local" };

        _context.Users.AddRange(superAdmin, otherUser, targetUser);

        // The target user performed some action in the past, leaving an AuditLog row that
        // references them (AuditLogs.UserId is NO ACTION, not CASCADE).
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = targetUser.Id,
            ActionCode = "LOGIN",
            TimeStamp = DateTimeOffset.UtcNow,
            Description = "Target user logged in",
            TableAffected = "Users"
        });

        // The target user also acknowledged/escalated a compliance alert that belongs to a
        // DIFFERENT user's compliance status (ComplianceAlerts.AcknowledgedBy/EscalatedTo are
        // NO ACTION), and changed/approved that other user's compliance history.
        var otherStatus = new ComplianceStatus
        {
            ComplianceStatusId = 1,
            UserId = otherUser.Id,
            OverallStatus = "Partial",
            RiskLevel = "Medium",
            ComplianceCategory = "Individual"
        };
        _context.ComplianceStatuses.Add(otherStatus);

        _context.ComplianceAlerts.Add(new ComplianceAlert
        {
            ComplianceStatusId = otherStatus.ComplianceStatusId,
            AlertType = "Missing",
            Severity = "Medium",
            AlertMessage = "Missing document",
            AcknowledgedBy = targetUser.Id,
            EscalatedTo = targetUser.Id
        });

        _context.ComplianceHistories.Add(new ComplianceHistory
        {
            ComplianceStatusId = otherStatus.ComplianceStatusId,
            PreviousStatus = "Pending",
            NewStatus = "Partial",
            ChangeReason = "Manual review",
            ChangedBy = targetUser.Id,
            ApprovedBy = targetUser.Id
        });

        await _context.SaveChangesAsync();

        var controller = CreateController(superAdmin);

        var result = await controller.DeleteUserById(targetUser.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _context.Users.FindAsync(targetUser.Id));

        var survivingAuditLog = await _context.AuditLogs.FirstAsync();
        Assert.Null(survivingAuditLog.UserId);

        var survivingAlert = await _context.ComplianceAlerts.FirstAsync();
        Assert.Null(survivingAlert.AcknowledgedBy);
        Assert.Null(survivingAlert.EscalatedTo);

        var survivingHistory = await _context.ComplianceHistories.FirstAsync();
        Assert.Null(survivingHistory.ChangedBy);
        Assert.Null(survivingHistory.ApprovedBy);
    }

    private UserController CreateController(User actingUser)
    {
        var userStore = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            MockBehavior.Loose,
            userStore.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(actingUser);
        userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .Returns<string>(id => _context.Users.FirstOrDefaultAsync(u => u.Id == id));
        userManager.Setup(x => x.IsInRoleAsync(actingUser, "Admin")).ReturnsAsync(false);
        userManager.Setup(x => x.IsInRoleAsync(actingUser, "Department Admin")).ReturnsAsync(false);
        // Actually remove the row (rather than just returning success) so the real Sqlite
        // database enforces its foreign keys on the delete, the way SQL Server would in prod -
        // that's the whole point of using Sqlite here instead of the EF InMemory provider.
        userManager.Setup(x => x.DeleteAsync(It.IsAny<User>())).Returns<User>(async u =>
        {
            _context.Users.Remove(u);
            await _context.SaveChangesAsync();
            return IdentityResult.Success;
        });

        var roleManager = new Mock<RoleManager<Role>>(
            new Mock<IRoleStore<Role>>().Object,
            Array.Empty<IRoleValidator<Role>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<Role>>.Instance);

        var tokenService = new Mock<ITokenService>();
        var signInManager = new Mock<SignInManager<User>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var auditLogService = new Mock<IAuditLogService>();
        var emailService = new Mock<IEmailService>();
        var fileScanService = new Mock<IFileScanService>();

        var controller = new UserController(
            userManager.Object,
            tokenService.Object,
            signInManager.Object,
            _context,
            roleManager.Object,
            new Mock<IEntityVerificationService>().Object,
            configuration,
            auditLogService.Object,
            emailService.Object,
            fileScanService.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actingUser.Id)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        return controller;
    }
}
