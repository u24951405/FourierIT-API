using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FourierIT.API.Tests;

public class InstitutionAuthTimerTests
{
    [Fact]
    public async Task ValidateToken_UsesConfiguredOtpExpiryMinutes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InstitutionAuthTimerTests_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Institutions.Add(new Institution
        {
            InstitutionId = 1,
            InstitutionName = "Test Institution",
            VerifiedDomain = "test.example",
            RegNumber = 123,
            TypeId = 1
        });
        context.InstitutionInvitations.Add(new InstitutionInvitation
        {
            InvitationId = 1,
            InstitutionId = 1,
            Email = "portal@test.example",
            TokenString = "token-123",
            TokenExpiryTimeStamp = DateTimeOffset.MaxValue,
            OtpExpiryTimeStamp = DateTimeOffset.UtcNow,
            OtpCodeHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SystemSettings.Add(new SystemSetting
        {
            Key = "InstitutionOtpExpiryMinutes",
            Value = "7",
            Description = "Test OTP duration"
        });
        await context.SaveChangesAsync();

        var emailService = new Mock<IEmailService>();
        emailService.Setup(service => service.SendInstitutionOtpEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(Task.CompletedTask);
        var controller = new InstitutionAuthController(
            context,
            NullLogger<InstitutionAuthController>.Instance,
            emailService.Object);
        var before = DateTimeOffset.UtcNow;

        var result = await controller.ValidateToken(new TokenValidationRequestDto
        {
            AccessToken = "token-123"
        });

        var invitation = await context.InstitutionInvitations.SingleAsync();
        var expiry = invitation.OtpExpiryTimeStamp;
        Assert.IsType<OkObjectResult>(result);
        Assert.InRange(expiry, before.AddMinutes(7).AddSeconds(-5), DateTimeOffset.UtcNow.AddMinutes(7).AddSeconds(5));
    }

    [Fact]
    public async Task ValidateToken_ForLinkPastItsExpiry_IsRefused()
    {
        await using var context = CreateContext(invitation => invitation.TokenExpiryTimeStamp = DateTimeOffset.UtcNow.AddMinutes(-1));
        var (controller, emailService) = CreateController(context);

        var result = await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });

        Assert.IsType<BadRequestObjectResult>(result);
        emailService.Verify(s => s.SendInstitutionOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task ValidateToken_ForOldLinkWithoutExpiry_ExpiresByCreationDate()
    {
        // Links created before expiry existed were stored with no expiry; they now expire 7 days after creation.
        await using var context = CreateContext(invitation => invitation.CreatedAt = DateTimeOffset.UtcNow.AddDays(-8));
        var (controller, _) = CreateController(context);

        var result = await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ValidateToken_ForRevokedLink_IsRefused()
    {
        await using var context = CreateContext(invitation => invitation.IsRevoked = true);
        var (controller, _) = CreateController(context);

        var result = await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ValidateToken_OpenedTwiceQuickly_SendsOnlyOneCode()
    {
        await using var context = CreateContext();
        var (controller, emailService) = CreateController(context);

        await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });
        var second = await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });

        Assert.IsType<OkObjectResult>(second);
        emailService.Verify(s => s.SendInstitutionOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task VerifyOtp_TooManyWrongCodes_CancelsTheCode()
    {
        await using var context = CreateContext();
        var (controller, emailService) = CreateController(context);
        string? sentCode = null;
        emailService.Setup(s => s.SendInstitutionOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, string, string, DateTimeOffset>((_, _, code, _) => sentCode = code)
            .Returns(Task.CompletedTask);
        await controller.ValidateToken(new TokenValidationRequestDto { AccessToken = "token-123" });
        var wrongCode = sentCode == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
            await controller.VerifyOtp(new OtpVerifyRequestDto { AccessToken = "token-123", InstitutionId = "1", Otp = wrongCode });

        // The real code no longer works once the limit (default 5) is reached.
        var withRealCode = await controller.VerifyOtp(new OtpVerifyRequestDto { AccessToken = "token-123", InstitutionId = "1", Otp = sentCode! });
        Assert.IsType<BadRequestObjectResult>(withRealCode);
        Assert.False((await context.InstitutionInvitations.SingleAsync()).IsUsed);
    }

    private static AppDbContext CreateContext(Action<InstitutionInvitation>? configure = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InstitutionAuthTimerTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Institutions.Add(new Institution
        {
            InstitutionId = 1,
            InstitutionName = "Test Institution",
            VerifiedDomain = "test.example",
            RegNumber = 123,
            TypeId = 1
        });
        var invitation = new InstitutionInvitation
        {
            InvitationId = 1,
            InstitutionId = 1,
            Email = "portal@test.example",
            TokenString = "token-123",
            TokenExpiryTimeStamp = DateTimeOffset.MaxValue,
            OtpExpiryTimeStamp = DateTimeOffset.UtcNow,
            OtpCodeHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };
        configure?.Invoke(invitation);
        context.InstitutionInvitations.Add(invitation);
        context.SaveChanges();
        return context;
    }

    private static (InstitutionAuthController Controller, Mock<IEmailService> EmailService) CreateController(AppDbContext context)
    {
        var emailService = new Mock<IEmailService>();
        emailService.Setup(s => s.SendInstitutionOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(Task.CompletedTask);
        var controller = new InstitutionAuthController(context, NullLogger<InstitutionAuthController>.Instance, emailService.Object);
        return (controller, emailService);
    }
}
