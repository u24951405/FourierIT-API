using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class UserControllerRegistrationOtpTests
{
    [Fact]
    public async Task Register_PendsUserUntilOtpIsVerified()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"UserControllerRegistrationOtpTests_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        if (!await context.EntityTypes.AnyAsync(e => e.EntityTypeId == 3))
        {
            context.EntityTypes.Add(new EntityType { EntityTypeId = 3, Name = "Business" });
            await context.SaveChangesAsync();
        }

        var userStore = new Mock<IUserStore<User>>();
        var createdUsers = new List<User>();
        var userManager = new Mock<UserManager<User>>(userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null, NullLogger<UserManager<User>>.Instance);

        userManager.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<User, string>((user, _) =>
            {
                user.Id = Guid.NewGuid().ToString("N");
                createdUsers.Add(user);
                context.Users.Add(user);
                context.SaveChanges();
            });
        userManager.Setup(x => x.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string? email) => createdUsers.FirstOrDefault(u => u.Email == email));
        userManager.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string? username) => createdUsers.FirstOrDefault(u => u.UserName == username));
        userManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<User>(user =>
            {
                var existing = createdUsers.FirstOrDefault(u => u.Id == user.Id);
                if (existing != null)
                {
                    existing.AccountStatus = user.AccountStatus;
                    existing.EmailVerified = user.EmailVerified;
                    existing.EmailVerificationCodeHash = user.EmailVerificationCodeHash;
                    existing.EmailVerificationExpiry = user.EmailVerificationExpiry;
                }
            });

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(x => x.CreateTokenAsync(It.IsAny<User>())).ReturnsAsync("token");

        var signInManager = new SignInManager<User>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        var entityVerificationService = new Mock<IEntityVerificationService>();
        entityVerificationService.Setup(x => x.VerifyEntityAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(EntityVerificationResult.Valid());

        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var auditLogService = new Mock<IAuditLogService>();
        var emailService = new Mock<IEmailService>();
        emailService.Setup(x => x.SendUserRegistrationOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(Task.CompletedTask);
        var fileScanService = new Mock<IFileScanService>();
        fileScanService.Setup(x => x.ScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(FileScanResult.Clean());

        var controller = new UserController(
            userManager.Object,
            tokenService.Object,
            signInManager,
            context,
            new Mock<RoleManager<Role>>(new Mock<IRoleStore<Role>>().Object, Array.Empty<IRoleValidator<Role>>(), new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), NullLogger<RoleManager<Role>>.Instance).Object,
            entityVerificationService.Object,
            configuration,
            auditLogService.Object,
            emailService.Object,
            fileScanService.Object);

        var registerResult = await controller.Register(new UserDto
        {
            FirstName = "Mpho",
            LastName = "Dlamini",
            DateOfBirth = new DateOnly(1990, 1, 2),
            PhoneNumber = "0821234567",
            JobTitle = "Developer",
            Username = "mpho",
            EmailAddress = "mpho@example.com",
            Password = "Password123!",
            EntityTypeId = 3,
            EntityIdentificationNumber = "1234567890",
            Roles = new List<string> { "Document Owner" }
        });

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(registerResult);
        if (objectResult.StatusCode == StatusCodes.Status500InternalServerError)
        {
            var problem = objectResult.Value as ProblemDetails;
            var message = problem != null
                ? $"ProblemDetails: title={problem.Title}, detail={problem.Detail}, status={problem.Status}"
                : objectResult.Value?.ToString() ?? "<null>";
            throw new InvalidOperationException($"Registration returned 500: {message}");
        }

        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
        Assert.NotNull(objectResult.Value);

        var pendingUser = createdUsers.Single(u => u.Email == "mpho@example.com");
        Assert.Equal("PendingVerification", pendingUser.AccountStatus);
        Assert.False(pendingUser.EmailVerified);

        var verificationCode = "123456";
        pendingUser.EmailVerificationCodeHash = new PasswordHasher<User>().HashPassword(pendingUser, verificationCode);
        pendingUser.EmailVerificationExpiry = DateTimeOffset.UtcNow.AddMinutes(15);

        var verifyResult = await controller.VerifyRegistrationOtp(new VerifyRegistrationOtpRequestDto
        {
            EmailAddress = "mpho@example.com",
            Otp = verificationCode
        });

        Assert.IsAssignableFrom<ObjectResult>(verifyResult);
        var verifiedUser = createdUsers.Single(u => u.Email == "mpho@example.com");
        Assert.True(verifiedUser.EmailVerified);
        Assert.Equal("Active", verifiedUser.AccountStatus);
    }
}
