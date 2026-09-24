using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FourierIT_API.DTOs;

namespace FourierIT_API.Exceptions
{
    /// <summary>
    /// Extension methods for controllers to handle deletion with proper error responses.
    /// </summary>
    public static class DeletionControllerExtension
    {
        /// <summary>
        /// Wraps a deletion operation with error handling.
        /// Returns appropriate HTTP status and error details if deletion fails.
        /// </summary>
        public static async Task<IActionResult> SafeDeleteAsync(
            this ControllerBase controller,
            string entityType,
            string entityId,
            Func<Task> deletionOperation)
        {
            try
            {
                await deletionOperation();
                return controller.Ok(new { message = $"{entityType} deleted successfully" });
            }
            catch (DbUpdateException dbEx) when (ContainsForeignKeyMessage(dbEx))
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(dbEx, entityType, entityId);
                return controller.Conflict(errorResponse);
            }
            catch (DeletionConflictException dce)
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(dce, entityType, dce.EntityId);
                return controller.Conflict(errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(ex, entityType, entityId);
                return controller.StatusCode(500, errorResponse);
            }
        }

        /// <summary>
        /// Wraps a deletion operation that returns a value.
        /// </summary>
        public static async Task<IActionResult> SafeDeleteAsync<T>(
            this ControllerBase controller,
            string entityType,
            string entityId,
            Func<Task<T>> deletionOperation)
        {
            try
            {
                var result = await deletionOperation();
                return controller.Ok(new { message = $"{entityType} deleted successfully", data = result });
            }
            catch (DbUpdateException dbEx) when (ContainsForeignKeyMessage(dbEx))
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(dbEx, entityType, entityId);
                return controller.Conflict(errorResponse);
            }
            catch (DeletionConflictException dce)
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(dce, entityType, dce.EntityId);
                return controller.Conflict(errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = DeletionErrorHandler.HandleDeletionError(ex, entityType, entityId);
                return controller.StatusCode(500, errorResponse);
            }
        }

        private static bool ContainsForeignKeyMessage(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                    || current.Message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
