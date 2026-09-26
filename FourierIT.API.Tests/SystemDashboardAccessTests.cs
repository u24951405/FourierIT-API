using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Compliance;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FourierIT.API.Tests;

public class SystemDashboardAccessTests
{
    [Fact]
    public async Task DepartmentAdmin_IsRefusedOrganisationWideFigures()
    {
        var result = await CreateController(new Claim(ClaimTypes.Role, "Department Admin")).GetSystemDashboard();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Stakeholder_CanViewOrganisationWideFigures()
    {
        var result = await CreateController(new Claim(ClaimTypes.Role, "Stakeholder")).GetSystemDashboard();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SuperAdmin_CanViewOrganisationWideFigures()
    {
        var result = await CreateController(new Claim("superadmin", "true")).GetSystemDashboard();

        Assert.IsType<OkObjectResult>(result);
    }

    private static ComplianceController CreateController(params Claim[] claims)
    {
        var complianceService = new Mock<IComplianceService>();
        complianceService.Setup(s => s.GetSystemDashboardAsync()).ReturnsAsync(new ComplianceDashboardDto());

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SystemDashboardAccessTests_{Guid.NewGuid():N}")
            .Options);

        return new ComplianceController(complianceService.Object, userManager.Object, NullLogger<ComplianceController>.Instance, context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims.Append(new Claim(ClaimTypes.NameIdentifier, "user-1")), "TestAuth"))
                }
            }
        };
    }
}
