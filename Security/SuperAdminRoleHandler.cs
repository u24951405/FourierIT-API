using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Threading.Tasks;

namespace FourierIT_API.Security
{
    /// <summary>
    /// Authorization handler that grants any Roles-based requirement when the seeded Super Admin account is authenticated.
    /// This allows the Super Admin to satisfy [Authorize(Roles = "...")] attributes without changing every controller.
    /// </summary>
    public class SuperAdminRoleHandler : AuthorizationHandler<RolesAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolesAuthorizationRequirement requirement)
        {
            if (context?.User == null) return Task.CompletedTask;

            // If user is the seeded Super Admin account, grant immediately.
            if (context.User.HasClaim("superadmin", "true"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (requirement.AllowedRoles != null)
            {
                foreach (var role in requirement.AllowedRoles)
                {
                    if (context.User.IsInRole(role))
                    {
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
