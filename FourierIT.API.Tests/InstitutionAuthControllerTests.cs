using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class InstitutionAuthControllerTests
{
    [Fact]
    public async Task ValidateToken_AfterSuccessfulOtpVerification_ReturnsBadRequest()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"InstitutionAuthControllerTests_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var institutionType = new InstitutionType
        {
            InstitutionTypeId = 99,
            InstitutionTypeName = "Test Institution Type"
        };

        var institution = new Institution
        {
            InstitutionId = 101,
            InstitutionName = "Example Institution",
            VerifiedDomain = "example.com",
            RegNumber = 123456,
            TypeId = institutionType.InstitutionTypeId,
            InstitutionType = institutionType
        };

        var invitation = new InstitutionInvitation
        {
            InvitationId = 1,
            InstitutionId = institution.InstitutionId,
            Email = "user@example.com",
            TokenString = "test-token-123",
            OtpCodeHash = string.Empty,
            TokenExpiryTimeStamp = DateTimeOffset.MaxValue,
            OtpExpiryTimeStamp = DateTimeOffset.UtcNow,
            IsRevoked = false,
            IsUsed = false,
            OtpSendCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.InstitutionInvitations.Add(invitation);
        await context.SaveChangesAsync();

        string? capturedOtp = null;
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, string, string, DateTimeOffset>((_, _, otp, _) => capturedOtp = otp)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<InstitutionAuthController>>();
        var controller = new InstitutionAuthController(context, loggerMock.Object, emailServiceMock.Object);

        var tokenValidationResult = await controller.ValidateToken(new TokenValidationRequestDto
        {
            AccessToken = invitation.TokenString
        });

        Assert.IsType<OkObjectResult>(tokenValidationResult);

        var verifyResult = await controller.VerifyOtp(new OtpVerifyRequestDto
        {
            AccessToken = invitation.TokenString,
            InstitutionId = institution.InstitutionId.ToString(),
            Otp = capturedOtp!
        });

        Assert.IsType<OkObjectResult>(verifyResult);

        var secondTokenValidationResult = await controller.ValidateToken(new TokenValidationRequestDto
        {
            AccessToken = invitation.TokenString
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(secondTokenValidationResult);
        var errorMessage = badRequest.Value?.GetType().GetProperty("error")?.GetValue(badRequest.Value)?.ToString();

        Assert.Equal("This invitation has already been used.", errorMessage);
    }
}
