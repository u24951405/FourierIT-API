using Microsoft.EntityFrameworkCore;
using FourierIT_API.DTOs;

namespace FourierIT_API.Exceptions
{
    public static class DeletionErrorHandler
    {
        /// <summary>
        /// Handles database deletion errors and returns appropriate error response.
        /// Catches foreign key constraint violations and provides user-friendly messages.
        /// </summary>
        public static ErrorResponseDto HandleDeletionError(Exception ex, string entityType, string entityId)
        {
            // Check for SQL Server foreign key constraint violation
            if (ex is DbUpdateException dbEx &&
                dbEx.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
            {
                var dependencies = ExtractForeignKeyDependencies(dbEx.InnerException.Message);
                return new DeletionConflictResponseDto(entityType, entityId, dependencies);
            }

            // Check for custom DeletionConflictException
            if (ex is DeletionConflictException dce)
            {
                return new DeletionConflictResponseDto(dce.EntityType, dce.EntityId, dce.ConflictingDependencies);
            }

            // Generic database error
            return new ErrorResponseDto
            {
                Error = "DELETION_ERROR",
                Message = $"Failed to delete {entityType}",
                Details = new List<string> { ex.Message },
                StatusCode = 500
            };
        }

        /// <summary>
        /// Extracts table/entity names from foreign key constraint violation message.
        /// This helps identify which dependent records are preventing deletion.
        /// </summary>
        private static List<string> ExtractForeignKeyDependencies(string errorMessage)
        {
            var dependencies = new List<string>();

            // Extract table names from error message patterns
            // Pattern: "FOREIGN KEY constraint "FK_TableName_..."
            var patterns = new[]
            {
                @"FK_(\w+)_",
                @"The DELETE statement conflicted with a FOREIGN KEY constraint ""(\w+)""",
                @"table ""(\w+)"""
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(errorMessage, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var tableName = match.Groups[1].Value;
                        dependencies.Add(ConvertTableNameToReadable(tableName));
                    }
                }
            }

            // If no dependencies found, provide a generic message
            if (dependencies.Count == 0)
            {
                dependencies.Add("Associated records in the database");
            }

            return dependencies.Distinct().ToList();
        }

        /// <summary>
        /// Converts database table names to readable entity names.
        /// Example: "DocumentAccesses" -> "Document Access Grants"
        /// </summary>
        private static string ConvertTableNameToReadable(string tableName)
        {
            var readableNames = new Dictionary<string, string>
            {
                { "Documents", "Documents" },
                { "DocumentAccessLogs", "Document Access Logs" },
                { "DocumentStatusHistories", "Document Status Records" },
                { "CertificationDetails", "Certification Details" },
                { "BlobHistories", "Document Version History" },
                { "Branches", "Branches" },
                { "Departments", "Departments" },
                { "InstitutionMembers", "Institution Members" },
                { "ClientEnlistments", "Client Enlistments" },
                { "ComplianceStatuses", "Compliance Records" },
                { "ComplianceHistories", "Compliance History" },
                { "ComplianceAlerts", "Compliance Alerts" },
                { "ComplianceAuditLogs", "Compliance Audit Logs" },
                { "UserSecurityQuestions", "Security Questions" },
                { "UserNotifications", "Notifications" },
                { "UserRoles", "User Roles" },
                { "RolePermissions", "Role Permissions" },
                { "AccessLists", "Access Lists" },
                { "EnquiryComments", "Enquiry Comments" },
                { "InstitutionEnquiryRequests", "Enquiry Requests" },
                { "RiskRatingVariables", "Risk Rating Variables" },
                { "DocumentComplianceChecks", "Compliance Checks" }
            };

            return readableNames.ContainsKey(tableName)
                ? readableNames[tableName]
                : SplitCamelCase(tableName);
        }

        /// <summary>
        /// Converts PascalCase to readable text.
        /// Example: "DocumentAccess" -> "Document Access"
        /// </summary>
        private static string SplitCamelCase(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                "(?<!^)(?=[A-Z])",
                " "
            );
        }
    }
}
