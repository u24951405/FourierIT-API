using System.Text.Json;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Institution;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class InstitutionPortalFixesTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"InstitutionPortalFixesTests_{Guid.NewGuid():N}")
        .Options);

    [Fact]
    public async Task RequestNewToken_EmailsTheConfiguredPortalAddress_NotTheCallersOrigin()
    {
        await using var context = CreateContext();
        context.Institutions.Add(new Institution { InstitutionId = 7, InstitutionName = "Example Bank", VerifiedDomain = "bank.test" });
        context.InstitutionInvitations.Add(new InstitutionInvitation
        {
            InstitutionId = 7,
            Email = "contact@bank.test",
            TokenString = "old-token",
            OtpCodeHash = string.Empty,
            TokenExpiryTimeStamp = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });
        await context.SaveChangesAsync();

        string? emailedLink = null;
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Callback<string, string, string, DateTimeOffset>((_, _, link, _) => emailedLink = link)
            .Returns(Task.CompletedTask);

        var controller = new InstitutionAuthController(context, Mock.Of<ILogger<InstitutionAuthController>>(), email.Object,
            emailSettings: Options.Create(new EmailSettings { FrontendBaseUrl = "https://portal.docuvault.test/" }));
        var httpContext = new DefaultHttpContext();
        // Anyone can call this endpoint, so the Origin header must not decide where the emailed link points.
        httpContext.Request.Headers["Origin"] = "https://look-alike.example";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.RequestNewToken(new RequestNewTokenRequestDto { InstitutionId = "7" });

        Assert.IsType<OkObjectResult>(result);
        Assert.StartsWith("https://portal.docuvault.test/institution/auth/access?token=", emailedLink);
    }

    [Fact]
    public async Task Logout_RevokesTheSessionOnTheServer()
    {
        await using var context = CreateContext();
        context.InstitutionSessionTokens.Add(new InstitutionSessionToken
        {
            InstitutionId = 7,
            TokenString = "session-1",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();
        var controller = new InstitutionAuthController(context, Mock.Of<ILogger<InstitutionAuthController>>(), Mock.Of<IEmailService>());

        Assert.IsType<NoContentResult>(await controller.Logout("session-1"));

        var session = await context.InstitutionSessionTokens.SingleAsync();
        Assert.True(session.IsRevoked);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task DepartmentDocumentTypes_OnlyOffersTypesTheDepartmentAccepts()
    {
        await using var context = CreateContext();
        context.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "Fourier IT Innovation" });
        context.DocumentTypes.AddRange(
            new DocumentType { DocumentTypeId = 11, TypeName = "Company Registration" },
            new DocumentType { DocumentTypeId = 12, TypeName = "Tax Clearance" });
        // The company requirements include type 12, but this department only handles type 11.
        context.RequiredDocuments.AddRange(
            new RequiredDocument { EntityTypeId = 3, DocumentTypeId = 11, IsMandatory = true },
            new RequiredDocument { EntityTypeId = 3, DocumentTypeId = 12, IsMandatory = true });
        context.DepartmentDocumentTypes.Add(new DepartmentDocumentType { DepartmentId = 1, DocumentTypeId = 11 });
        context.InstitutionSessionTokens.Add(new InstitutionSessionToken
        {
            InstitutionId = 7,
            TokenString = "session-1",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();

        var result = await CreateRequestsController(context).GetInstitutionRecipientDocumentTypes("session-1", "Department", "1");

        var ok = Assert.IsType<OkObjectResult>(result);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var offered = json.RootElement.GetProperty("documentTypes").EnumerateArray().ToList();
        var only = Assert.Single(offered);
        Assert.Equal(11, only.GetProperty("documentTypeId").GetInt32());
        Assert.True(only.GetProperty("isMandatory").GetBoolean());
    }

    [Fact]
    public void AccessTokens_AllowOneOwnerToApproveManyRequests()
    {
        using var context = CreateContext();
        var userIdIndex = context.Model.FindEntityType(typeof(AccessToken))!
            .GetIndexes()
            .Single(index => index.Properties.Single().Name == nameof(AccessToken.UserId));

        Assert.False(userIdIndex.IsUnique);
    }

    private static DocumentAccessRequestsController CreateRequestsController(AppDbContext context)
    {
        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, NullLogger<UserManager<User>>.Instance);
        return new DocumentAccessRequestsController(
            context,
            userManager.Object,
            Mock.Of<IDocumentService>(),
            new FourierIT_API.Services.DepartmentRequestValidationService(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IComplianceService>());
    }
}
