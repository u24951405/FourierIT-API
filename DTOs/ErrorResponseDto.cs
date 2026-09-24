namespace FourierIT_API.DTOs
{
    public class ErrorResponseDto
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new List<string>();
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DeletionConflictResponseDto : ErrorResponseDto
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public List<string> ConflictingDependencies { get; set; } = new List<string>();
        public string Suggestion { get; set; } = string.Empty;

        public DeletionConflictResponseDto(string entityType, string entityId, List<string> dependencies)
        {
            Error = "DELETION_CONFLICT";
            Message = BuildMessage(entityType, dependencies);
            EntityType = entityType;
            EntityId = entityId;
            ConflictingDependencies = dependencies;
            Suggestion = BuildSuggestion(entityType, dependencies);
            Details = dependencies.Select(dependency => $"Related record: {dependency}").ToList();
            StatusCode = 409;
        }

        private static string BuildMessage(string entityType, List<string> dependencies)
        {
            if (string.Equals(entityType, "Role", StringComparison.OrdinalIgnoreCase))
            {
                return "This role cannot be deleted because it is still being used.";
            }

            if (string.Equals(entityType, "User", StringComparison.OrdinalIgnoreCase)
                && dependencies.Count > 0)
            {
                return $"This user cannot be deleted because of: {string.Join(", ", dependencies)}.";
            }

            return $"This {entityType.ToLowerInvariant()} cannot be deleted because it is still being used by other records.";
        }

        private static string BuildSuggestion(string entityType, List<string> dependencies)
        {
            if (string.Equals(entityType, "Role", StringComparison.OrdinalIgnoreCase))
            {
                if (dependencies.Any(dependency => dependency.StartsWith("Assigned to ", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Unassign this role from all users before deleting it.";
                }

                if (dependencies.Any(dependency => string.Equals(dependency, "Role Permissions", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Remove all permissions from this role before deleting it.";
                }

                return "Remove this role's related records before deleting it.";
            }

            return $"Review the related records ({string.Join(", ", dependencies)}) and remove or reassign them before deleting this {entityType.ToLowerInvariant()}.";
        }
    }
}
