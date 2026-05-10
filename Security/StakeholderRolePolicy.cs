namespace FourierIT_API.Security;

/// <summary>
/// Stakeholder is mutually exclusive with other roles: a user cannot hold Stakeholder and another role at the same time.
/// </summary>
public static class StakeholderRolePolicy
{
    public const string StakeholderRoleName = "Stakeholder";

    public static bool ViolatesStakeholderExclusivity(IEnumerable<string>? roleNames)
    {
        if (roleNames == null) return false;
        var list = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasStakeholder = list.Any(r => string.Equals(r, StakeholderRoleName, StringComparison.OrdinalIgnoreCase));
        return hasStakeholder && list.Count > 1;
    }

    /// <summary>
    /// Validates adding a single role to a user who currently has <paramref name="currentRoles"/>.
    /// </summary>
    public static string? ValidateAddRole(IEnumerable<string> currentRoles, string roleToAdd)
    {
        var add = roleToAdd.Trim();
        var current = (currentRoles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToList();

        var hasStakeholder = current.Any(r => string.Equals(r, StakeholderRoleName, StringComparison.OrdinalIgnoreCase));
        var addingStakeholder = string.Equals(add, StakeholderRoleName, StringComparison.OrdinalIgnoreCase);

        if (hasStakeholder && !addingStakeholder)
            return "Remove the Stakeholder role before assigning another role.";

        if (addingStakeholder && current.Count > 0)
            return "Remove existing role(s) before assigning Stakeholder.";

        return null;
    }

    /// <summary>
    /// Simulates replace(oldRole -&gt; newRole) and returns an error message if the result would violate exclusivity.
    /// </summary>
    public static string? ValidateReplaceRole(IEnumerable<string> currentRoles, string oldRole, string newRole)
    {
        var oldR = oldRole.Trim();
        var newR = newRole.Trim();

        var simulated = (currentRoles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Where(r => !string.Equals(r, oldR, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!simulated.Any(r => string.Equals(r, newR, StringComparison.OrdinalIgnoreCase)))
            simulated.Add(newR);

        if (ViolatesStakeholderExclusivity(simulated))
            return "Stakeholder cannot be combined with other roles. Remove a role first.";

        return null;
    }
}
