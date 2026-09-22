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

        public DeletionConflictResponseDto(string entityType, string entityId, List<string> dependencies)
        {
            Error = "DELETION_CONFLICT";
            Message = $"Cannot delete {entityType} because it has dependent records";
            EntityType = entityType;
            EntityId = entityId;
            ConflictingDependencies = dependencies;
            Details = new List<string>
            {
                $"This {entityType} is associated with:",
                string.Join("\n", dependencies.Select(d => $"  • {d}"))
            };
            StatusCode = 409;
        }
    }
}
