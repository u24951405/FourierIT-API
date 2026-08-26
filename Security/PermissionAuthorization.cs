using System.Security.Claims;
using FourierIT_API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Security;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionKey)
    {
        PermissionKey = permissionKey;
    }

    public string PermissionKey { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _context;

    public PermissionAuthorizationHandler(AppDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext authorizationContext,
        PermissionRequirement requirement)
    {
        if (authorizationContext.User.HasClaim("superadmin", "true"))
        {
            authorizationContext.Succeed(requirement);
            return;
        }

        var userId = authorizationContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? authorizationContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var hasPermission = await _context.Set<IdentityUserRole<string>>()
            .Where(userRole => userRole.UserId == userId)
            .Join(
                _context.RolePermissions,
                userRole => userRole.RoleId,
                rolePermission => rolePermission.RoleId,
                (_, rolePermission) => rolePermission.PermissionId)
            .Join(
                _context.Permissions,
                permissionId => permissionId,
                permission => permission.PermissionId,
                (_, permission) => permission.PermissionKey)
            .AnyAsync(permissionKey => permissionKey == requirement.PermissionKey);

        if (hasPermission)
            authorizationContext.Succeed(requirement);
    }
}
