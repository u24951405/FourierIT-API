using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>User Management only changes a user's role; personal details stay with the user.</summary>
public class ChangeUserRoleTests
{
    private static readonly string[] AllRoles = { "Admin", "Department Admin", "Document Owner", "Compliance Officer", "Stakeholder" };

    [Fact]
    public async Task SuperAdmin_ReplacesTheUsersRole()
    {
        var fixture = await CreateFixtureAsync(targetRoles: new[] { "Stakeholder" });

        var result = await fixture.Controller.ChangeUserRole(fixture.Target.Id, new ChangeUserRoleDto { Role = "Compliance Officer" });

        Assert.IsType<OkObjectResult>(result);
        fixture.UserManager.Verify(m => m.RemoveFromRolesAsync(fixture.Target, It.Is<IEnumerable<string>>(r => r.Single() == "Stakeholder")), Times.Once);
        fixture.UserManager.Verify(m => m.AddToRoleAsync(fixture.Target, "Compliance Officer"), Times.Once);
    }

    [Fact]
    public async Task DepartmentAdmin_CannotChangeRoles()
    {
        var fixture = await CreateFixtureAsync(targetRoles: new[] { "Stakeholder" }, actingAsSuperAdmin: false, actingRole: "Department Admin");

        var result = await fixture.Controller.ChangeUserRole(fixture.Target.Id, new ChangeUserRoleDto { Role = "Compliance Officer" });

        Assert.IsType<ForbidResult>(result);
        fixture.UserManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BecomingADocumentOwner_NeedsAnEntityType()
    {
        var fixture = await CreateFixtureAsync(targetRoles: new[] { "Stakeholder" });

        var result = await fixture.Controller.ChangeUserRole(fixture.Target.Id, new ChangeUserRoleDto { Role = "Document Owner" });

        Assert.Contains("entity type", ErrorOf(result));
        fixture.UserManager.Verify(m => m.RemoveFromRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task AnAssignedDepartmentAdmin_MustBeUnassignedFirst()
    {
        var fixture = await CreateFixtureAsync(targetRoles: new[] { "Department Admin" }, targetDepartmentId: 3);

        var result = await fixture.Controller.ChangeUserRole(fixture.Target.Id, new ChangeUserRoleDto { Role = "Stakeholder" });

        Assert.Contains("Unassign", ErrorOf(result));
    }

    [Fact]
    public async Task SameRole_IsRejectedWithAClearMessage()
    {
        var fixture = await CreateFixtureAsync(targetRoles: new[] { "Stakeholder" });

        var result = await fixture.Controller.ChangeUserRole(fixture.Target.Id, new ChangeUserRoleDto { Role = "stakeholder" });

        Assert.Contains("already a Stakeholder", ErrorOf(result));
    }

    private static string ErrorOf(IActionResult result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        return bad.Value?.GetType().GetProperty("error")?.GetValue(bad.Value)?.ToString() ?? string.Empty;
    }

    private sealed record Fixture(UserController Controller, User Target, Mock<UserManager<User>> UserManager);

    private static async Task<Fixture> CreateFixtureAsync(string[] targetRoles, bool actingAsSuperAdmin = true, string? actingRole = null, int? targetDepartmentId = null)
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ChangeUserRoleTests_{Guid.NewGuid():N}")
            .Options);
        var acting = new User { Id = "acting", UserName = actingAsSuperAdmin ? "superadmin" : "deptadmin", Email = "acting@test.local" };
        var target = new User { Id = "target", UserName = "target", Email = "target@test.local", DepartmentId = targetDepartmentId };
        context.Users.AddRange(acting, target);
        await context.SaveChangesAsync();

        var userManager = new Mock<UserManager<User>>(
            MockBehavior.Loose,
            new Mock<IUserStore<User>>().Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(acting);
        userManager.Setup(m => m.FindByIdAsync(acting.Id)).ReturnsAsync(acting);
        userManager.Setup(m => m.FindByIdAsync(target.Id)).ReturnsAsync(target);
        userManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(targetRoles.ToList());
        userManager.Setup(m => m.RemoveFromRolesAsync(target, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(target, It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var roleManager = new Mock<RoleManager<Role>>(
            new Mock<IRoleStore<Role>>().Object,
            Array.Empty<IRoleValidator<Role>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<Role>>.Instance);
        roleManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => AllRoles.FirstOrDefault(r => r.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } match
                ? new Role { Name = match, NormalizedName = match.ToUpperInvariant() }
                : null);

        var signInManager = new Mock<SignInManager<User>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        var controller = new UserController(
            userManager.Object,
            Mock.Of<ITokenService>(),
            signInManager.Object,
            context,
            roleManager.Object,
            Mock.Of<IEntityVerificationService>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IFileScanService>());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, acting.Id) };
        if (actingAsSuperAdmin) claims.Add(new Claim("superadmin", "true"));
        if (actingRole != null) claims.Add(new Claim(ClaimTypes.Role, actingRole));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        return new Fixture(controller, target, userManager);
    }
}
