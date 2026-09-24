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

/// <summary>
/// Department Admin previously had explicit in-method allowances to edit or delete any
/// user (UpdateManagedUser/DeleteManagedUser/DeleteUserById in UserController), which was
/// restricted to only Admin/Super Admin, then partially reopened: Department Admin should
/// be able to delete any user (just not edit them). Institution mutation endpoints were
/// also loosely gated by the broad "Users.Manage" permission, which Department Admin holds;
/// they're now restricted to the "Admin" role via [Authorize(Roles = "Admin")], which the
/// SuperAdminRoleHandler still bypasses for the seeded Super Admin claim.
/// </summary>
public class DepartmentAdminRestrictionTests
{
    [Fact]
    public async Task UpdateManagedUser_ByDepartmentAdmin_ReturnsForbidden()
    {
        var (controller, targetProfile) = await CreateFixtureAsync(actingRole: "Department Admin");

        var result = await controller.UpdateManagedUser(targetProfile.ProfileId, ValidUpdateDto());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateManagedUser_ByAdmin_Succeeds()
    {
        var (controller, targetProfile) = await CreateFixtureAsync(actingRole: "Admin");

        var result = await controller.UpdateManagedUser(targetProfile.ProfileId, ValidUpdateDto());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeleteManagedUser_ByDepartmentAdmin_Succeeds()
    {
        var (controller, targetProfile) = await CreateFixtureAsync(actingRole: "Department Admin");

        var result = await controller.DeleteManagedUser(targetProfile.ProfileId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteUserById_ByDepartmentAdmin_Succeeds()
    {
        var (controller, _) = await CreateFixtureAsync(actingRole: "Department Admin", targetUserId: "target-user");

        var result = await controller.DeleteUserById("target-user");

        Assert.IsType<NoContentResult>(result);
    }

    private static UpdateUserManagementRequestDto ValidUpdateDto() => new()
    {
        FirstName = "Updated",
        LastName = "Name",
        DateOfBirth = new DateOnly(1990, 1, 1),
        PhoneNumber = "0123456789",
        JobTitle = "Tester",
        EmailAddress = "target@test.local",
        Role = "Stakeholder",
        AccountStatus = "Active"
    };

    private static async Task<(UserController Controller, Profile TargetProfile)> CreateFixtureAsync(
        string actingRole, string targetUserId = "target-user")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepartmentAdminRestrictionTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);

        var actingUser = new User { Id = "acting-user", UserName = "acting@test.local", Email = "acting@test.local" };
        var targetUser = new User { Id = targetUserId, UserName = "target@test.local", Email = "target@test.local" };
        var targetProfile = new Profile
        {
            ProfileId = 1,
            UserId = targetUser.Id,
            User = targetUser,
            FirstName = "Original",
            LastName = "Name",
            PhoneNumber = "0000000000",
            JobTitle = "Original title"
        };

        context.Users.AddRange(actingUser, targetUser);
        context.Profiles.Add(targetProfile);
        context.Roles.Add(new Role { Id = "SH-role", Name = "Stakeholder", NormalizedName = "STAKEHOLDER" });
        await context.SaveChangesAsync();

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
            null!,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(actingUser);
        userManager.Setup(x => x.FindByIdAsync(actingUser.Id)).ReturnsAsync(actingUser);
        userManager.Setup(x => x.FindByIdAsync(targetUser.Id)).ReturnsAsync(targetUser);
        userManager.Setup(x => x.IsInRoleAsync(actingUser, "Admin")).ReturnsAsync(actingRole == "Admin");
        userManager.Setup(x => x.IsInRoleAsync(actingUser, "Department Admin")).ReturnsAsync(actingRole == "Department Admin");
        userManager.Setup(x => x.GetRolesAsync(targetUser)).ReturnsAsync(new List<string>());
        userManager.Setup(x => x.RemoveFromRolesAsync(targetUser, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.AddToRoleAsync(targetUser, It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.DeleteAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        var roleManager = new Mock<RoleManager<Role>>(
            new Mock<IRoleStore<Role>>().Object,
            Array.Empty<IRoleValidator<Role>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<Role>>.Instance);
        roleManager.Setup(x => x.RoleExistsAsync("Stakeholder")).ReturnsAsync(true);

        var tokenService = new Mock<ITokenService>();
        var signInManager = new Mock<SignInManager<User>>(
            userManager.Object,
            Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
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
            context,
            roleManager.Object,
            new Mock<IEntityVerificationService>().Object,
            configuration,
            auditLogService.Object,
            emailService.Object,
            fileScanService.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actingUser.Id),
            new(ClaimTypes.Role, actingRole)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        return (controller, targetProfile);
    }
}
