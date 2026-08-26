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
            Value = "3",
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
        Assert.InRange(expiry, before.AddMinutes(3).AddSeconds(-5), DateTimeOffset.UtcNow.AddMinutes(3).AddSeconds(5));
    }
}
