namespace FourierIT_API.Exceptions
{
    public class DeletionConflictException : Exception
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public List<string> ConflictingDependencies { get; set; }

        public DeletionConflictException(
            string entityType,
            string entityId,
            List<string> conflictingDependencies)
            : base(BuildMessage(entityType, conflictingDependencies))
        {
            EntityType = entityType;
            EntityId = entityId;
            ConflictingDependencies = conflictingDependencies;
        }

        private static string BuildMessage(string entityType, List<string> dependencies)
        {
            return $"Cannot delete {entityType} because it has associated records: " +
                   string.Join(", ", dependencies);
        }
    }
}
