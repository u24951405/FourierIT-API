using System.Security.Claims;
using System.Reflection;
using FourierIT_API.Data;
using FourierIT_API.Security;
using FourierIT_API.Models;
using FourierIT_API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FourierIT.API.Tests;

public class PermissionAuthorizationTests
{
    [Fact]
    public async Task RolePermission_GrantsConfiguredPermissionOnly()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PermissionAuthorizationTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.Permissions.Add(new Permission { PermissionId = 1, PermissionKey = "Documents.Upload" });
        context.Permissions.Add(new Permission { PermissionId = 2, PermissionKey = "Compliance.View" });
        context.Permissions.Add(new Permission { PermissionId = 3, PermissionKey = "Documents.Manage" });
        context.Permissions.Add(new Permission { PermissionId = 4, PermissionKey = "Reports.View" });
        context.UserRoles.Add(new UserRole { UserId = "user-1", RoleId = "custom-role" });
        context.RolePermissions.Add(new RolePermission { RoleId = "custom-role", PermissionId = 1 });
        context.RolePermissions.Add(new RolePermission { RoleId = "custom-role", PermissionId = 2 });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddAuthorizationBuilder()
            .AddPolicy("Documents.Upload", policy => policy.Requirements.Add(new PermissionRequirement("Documents.Upload")))
            .AddPolicy("Compliance.View", policy => policy.Requirements.Add(new PermissionRequirement("Compliance.View")))
            .AddPolicy("Documents.Manage", policy => policy.Requirements.Add(new PermissionRequirement("Documents.Manage")))
            .AddPolicy("Reports.View", policy => policy.Requirements.Add(new PermissionRequirement("Reports.View")));
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        }, "test"));

        var upload = await authorization.AuthorizeAsync(principal, null, "Documents.Upload");
        var complianceView = await authorization.AuthorizeAsync(principal, null, "Compliance.View");
        var manage = await authorization.AuthorizeAsync(principal, null, "Documents.Manage");
        var reportsView = await authorization.AuthorizeAsync(principal, null, "Reports.View");

        Assert.True(upload.Succeeded);
        Assert.True(complianceView.Succeeded);
        Assert.False(manage.Succeeded);
        Assert.False(reportsView.Succeeded);

        var uploadPolicy = typeof(DocumentController).GetMethod("Upload")!
            .GetCustomAttributes<AuthorizeAttribute>().Single(attribute => attribute.Policy != null).Policy;
        var compliancePolicy = typeof(ComplianceController).GetMethod("GetDocumentIssues")!
            .GetCustomAttributes<AuthorizeAttribute>().Single(attribute => attribute.Policy != null).Policy;
        var reportsPolicy = typeof(ReportsController).GetMethod("GetDocumentOwnerComplianceReport")!
            .DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().Single(attribute => attribute.Policy != null).Policy;

        Assert.Equal("Documents.Upload", uploadPolicy);
        Assert.Equal("Compliance.View", compliancePolicy);
        Assert.Equal("Reports.View", reportsPolicy);
    }
}
