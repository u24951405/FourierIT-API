using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FourierIT.API.Tests;

public class ComplianceApplicabilityTests
{
    [Theory]
    [InlineData("Stakeholder")]
    [InlineData("Compliance Officer")]
    public async Task CheckUserCompliance_ForRoleThatDoesNotUploadDocuments_IsNotApplicableAndSavesNothing(string role)
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ComplianceApplicabilityTests_{Guid.NewGuid():N}")
            .Options);
        var user = new User { Id = "user-1", UserName = "viewer", Email = "viewer@test.local" };

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { role });

        var service = new ComplianceService(
            context,
            userManager.Object,
            NullLogger<ComplianceService>.Instance,
            new Mock<IAuditLogService>().Object);

        var status = await service.CheckUserComplianceAsync(user.Id);

        Assert.Equal(ComplianceService.NotApplicableStatus, status.OverallStatus);
        Assert.False(await context.ComplianceStatuses.AnyAsync());
        Assert.False(await context.ComplianceAlerts.AnyAsync());
    }
}
