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

public class EmailChangeTests
{
    private const string OldEmail = "old@test.local";
    private const string NewEmail = "new@test.local";

    [Fact]
    public async Task RequestThenVerify_WithCorrectCode_ChangesEmailAndNotifiesOldAddress()
    {
        var setup = CreateSetup();

        var request = await setup.Controller.RequestEmailChange(new RequestEmailChangeDto { NewEmail = NewEmail });
        Assert.IsType<OkObjectResult>(request);
        Assert.NotNull(setup.SentCode);
        setup.EmailService.Verify(x => x.SendEmailChangeOtpEmailAsync(NewEmail, It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Once);

        var verify = await setup.Controller.VerifyEmailChange(new VerifyEmailChangeDto { Otp = setup.SentCode! });

        Assert.IsType<OkObjectResult>(verify);
        Assert.Equal(NewEmail, setup.User.Email);
        Assert.Equal(NewEmail.ToUpperInvariant(), setup.User.NormalizedEmail);
        Assert.False(await setup.Context.PendingEmailChanges.AnyAsync());
        setup.EmailService.Verify(x => x.SendEmailChangedNoticeAsync(OldEmail, NewEmail), Times.Once);
        setup.TokenService.Verify(x => x.CreateTokenAsync(setup.User), Times.Once);
    }

    [Fact]
    public async Task Verify_WithWrongCodeTooManyTimes_DiscardsTheRequestAndKeepsOldEmail()
    {
        var setup = CreateSetup();
        await setup.Controller.RequestEmailChange(new RequestEmailChangeDto { NewEmail = NewEmail });
        var wrongCode = setup.SentCode == "000000" ? "111111" : "000000";

        IActionResult? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
            last = await setup.Controller.VerifyEmailChange(new VerifyEmailChangeDto { Otp = wrongCode });

        Assert.IsType<BadRequestObjectResult>(last);
        Assert.Equal(OldEmail, setup.User.Email);
        Assert.False(await setup.Context.PendingEmailChanges.AnyAsync());

        // The real code no longer works either: the request was discarded.
        var afterLockout = await setup.Controller.VerifyEmailChange(new VerifyEmailChangeDto { Otp = setup.SentCode! });
        Assert.IsType<BadRequestObjectResult>(afterLockout);
        Assert.Equal(OldEmail, setup.User.Email);
    }

    [Fact]
    public async Task Request_ForEmailUsedByAnotherAccount_IsRejected()
    {
        var setup = CreateSetup(emailTakenByOtherUser: true);

        var result = await setup.Controller.RequestEmailChange(new RequestEmailChangeDto { NewEmail = NewEmail });

        Assert.IsType<ConflictObjectResult>(result);
        setup.EmailService.Verify(x => x.SendEmailChangeOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Request_AgainWithinCooldown_IsThrottledEvenAfterCancelling()
    {
        var setup = CreateSetup();
        await setup.Controller.RequestEmailChange(new RequestEmailChangeDto { NewEmail = NewEmail });
        await setup.Controller.CancelEmailChange();

        var second = await setup.Controller.RequestEmailChange(new RequestEmailChangeDto { NewEmail = NewEmail });

        var throttled = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, throttled.StatusCode);
        setup.EmailService.Verify(x => x.SendEmailChangeOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Once);
    }

    private sealed class Setup
    {
        public required UserController Controller { get; init; }
        public required AppDbContext Context { get; init; }
        public required User User { get; init; }
        public required Mock<IEmailService> EmailService { get; init; }
        public required Mock<ITokenService> TokenService { get; init; }
        public string? SentCode { get; set; }
    }

    private static Setup CreateSetup(bool emailTakenByOtherUser = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"EmailChangeTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);

        var user = new User
        {
            Id = "user-1",
            UserName = "user1",
            Email = OldEmail,
            NormalizedEmail = OldEmail.ToUpperInvariant(),
            AccountStatus = "Active"
        };
        context.Users.Add(user);
        context.SaveChanges();

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
        userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(x => x.NormalizeEmail(It.IsAny<string?>())).Returns<string?>(email => email?.ToUpperInvariant());
        userManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((string email) => emailTakenByOtherUser && email == NewEmail
                ? new User { Id = "someone-else", Email = NewEmail }
                : null);
        userManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        var roleManager = new Mock<RoleManager<Role>>(
            new Mock<IRoleStore<Role>>().Object,
            Array.Empty<IRoleValidator<Role>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<Role>>.Instance);

        var signInManager = new Mock<SignInManager<User>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>());

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(x => x.CreateTokenAsync(It.IsAny<User>())).ReturnsAsync("new-token");

        var emailService = new Mock<IEmailService>();
        var controller = new UserController(
            userManager.Object,
            tokenService.Object,
            signInManager.Object,
            context,
            roleManager.Object,
            new Mock<IEntityVerificationService>().Object,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new Mock<IAuditLogService>().Object,
            emailService.Object,
            new Mock<IFileScanService>().Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "TestAuth"))
            }
        };

        var setup = new Setup
        {
            Controller = controller,
            Context = context,
            User = user,
            EmailService = emailService,
            TokenService = tokenService
        };
        emailService
            .Setup(x => x.SendEmailChangeOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, string, DateTimeOffset>((_, code, _) => setup.SentCode = code)
            .Returns(Task.CompletedTask);
        return setup;
    }
}
