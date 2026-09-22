# Deletion Error Handling Guide

This guide explains how to implement deletion error handling in your controllers to gracefully handle foreign key constraint violations.

## Architecture

### Components Created

1. **DeletionConflictException** - Custom exception for deletion conflicts
2. **ErrorResponseDto** - Standardized error response format
3. **DeletionErrorHandler** - Utility to parse database errors
4. **DeletionControllerExtension** - Extension methods for controllers

## Implementation Examples

### Example 1: Delete User

```csharp
[HttpDelete("{userId}")]
public async Task<IActionResult> DeleteUser(string userId)
{
    return await this.SafeDeleteAsync(
        "User",
        userId,
        async () =>
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            await _userRepository.DeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }
    );
}
```

**Response on Success (200):**
```json
{
    "message": "User deleted successfully"
}
```

**Response on Deletion Conflict (409):**
```json
{
    "error": "DELETION_CONFLICT",
    "message": "Cannot delete User because it has dependent records",
    "entityType": "User",
    "entityId": "user-123",
    "conflictingDependencies": [
        "Documents",
        "User Roles",
        "Notifications"
    ],
    "details": [
        "This User is associated with:",
        "  • Documents",
        "  • User Roles",
        "  • Notifications"
    ],
    "statusCode": 409,
    "timestamp": "2026-09-20T10:30:00Z"
}
```

---

### Example 2: Delete Department

```csharp
[HttpDelete("{departmentId}")]
[Authorize(Policy = "Roles.Manage")]
public async Task<IActionResult> DeleteDepartment(string departmentId)
{
    return await this.SafeDeleteAsync(
        "Department",
        departmentId,
        async () =>
        {
            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null)
                throw new KeyNotFoundException($"Department with ID {departmentId} not found");

            // Additional validation
            if (department.Children.Any())
                throw new DeletionConflictException(
                    "Department",
                    departmentId,
                    new List<string> { "Child Departments" }
                );

            await _departmentRepository.DeleteAsync(department);
            await _unitOfWork.SaveChangesAsync();
        }
    );
}
```

---

### Example 3: Delete Institution

```csharp
[HttpDelete("{institutionId}")]
[Authorize(Policy = "Users.Manage")]
public async Task<IActionResult> DeleteInstitution(string institutionId)
{
    return await this.SafeDeleteAsync(
        "Institution",
        institutionId,
        async () =>
        {
            var institution = await _context.Institutions
                .Include(i => i.Branches)
                .Include(i => i.ClientEnlistments)
                .Include(i => i.InstitutionMembers)
                .FirstOrDefaultAsync(i => i.InstitutionId == institutionId);

            if (institution == null)
                throw new KeyNotFoundException($"Institution with ID {institutionId} not found");

            await _institutionRepository.DeleteAsync(institution);
            await _unitOfWork.SaveChangesAsync();
        }
    );
}
```

---

### Example 4: Delete DocumentType

```csharp
[HttpDelete("{documentTypeId}")]
[Authorize(Policy = "Documents.Manage")]
public async Task<IActionResult> DeleteDocumentType(int documentTypeId)
{
    return await this.SafeDeleteAsync(
        "DocumentType",
        documentTypeId.ToString(),
        async () =>
        {
            var docType = await _context.DocumentTypes
                .FirstOrDefaultAsync(dt => dt.DocumentTypeId == documentTypeId);

            if (docType == null)
                throw new KeyNotFoundException($"Document Type with ID {documentTypeId} not found");

            _context.DocumentTypes.Remove(docType);
            await _context.SaveChangesAsync();
        }
    );
}
```

---

## Step-by-Step Implementation

### Step 1: Import the Extension
```csharp
using FourierIT_API.Exceptions;
```

### Step 2: Use SafeDeleteAsync in Your Delete Endpoints

Replace existing try-catch blocks with the extension method:

**Before:**
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(string id)
{
    try
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            return NotFound();

        await _repository.DeleteAsync(entity);
        return Ok(new { message = "Deleted successfully" });
    }
    catch (DbUpdateException ex)
    {
        return StatusCode(500, new { error = "Database error", message = ex.Message });
    }
}
```

**After:**
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(string id)
{
    return await this.SafeDeleteAsync(
        "Entity",
        id,
        async () =>
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                throw new KeyNotFoundException($"Entity with ID {id} not found");

            await _repository.DeleteAsync(entity);
        }
    );
}
```

## HTTP Status Codes

| Status | Meaning | When Used |
|--------|---------|-----------|
| 200 | OK | Deletion succeeded |
| 409 | Conflict | Foreign key constraint violation (dependent records exist) |
| 404 | Not Found | Entity doesn't exist |
| 500 | Server Error | Unexpected database error |

## Controllers to Update

### Critical (Update First)
- [ ] UserController.cs - DeleteUser endpoint
- [ ] DepartmentController.cs - DeleteDepartment endpoint
- [ ] InstitutionController.cs - DeleteInstitution endpoint
- [ ] BranchController.cs - DeleteBranch endpoint

### High Priority
- [ ] DocumentController.cs - DeleteDocument endpoint
- [ ] RolesController.cs - DeleteRole endpoint

### Medium Priority
- [ ] All other delete endpoints

## Error Response Structure

```json
{
    "error": "DELETION_CONFLICT",
    "message": "User-friendly message",
    "entityType": "The entity being deleted",
    "entityId": "ID of the entity",
    "conflictingDependencies": ["List of dependent records"],
    "details": ["Detailed explanation"],
    "statusCode": 409,
    "timestamp": "ISO 8601 timestamp"
}
```

## Testing

### Test Case 1: Successful Deletion
```csharp
// DELETE /api/departments/empty-dept
// Expected: 200 OK, "Department deleted successfully"
```

### Test Case 2: Deletion Conflict
```csharp
// DELETE /api/departments/dept-with-branches
// Expected: 409 Conflict with dependency list
```

### Test Case 3: Not Found
```csharp
// DELETE /api/departments/nonexistent-id
// Expected: 404 Not Found
```

## Frontend Integration

When receiving a 409 Conflict response:

```typescript
// Angular/TypeScript example
try {
    await this.http.delete(`/api/users/${userId}`).toPromise();
    this.toast.show('User deleted successfully', 'success');
} catch (error) {
    if (error.status === 409) {
        const conflictData = error.error;
        this.toast.show(
            `Cannot delete: This ${conflictData.entityType} has ${conflictData.conflictingDependencies.join(', ')}`,
            'error'
        );
    } else {
        this.toast.show('Failed to delete', 'error');
    }
}
```

## Database Migrations

After implementing error handling, run:

```bash
# Create migration for deletion protection
dotnet ef migrations add AddDeletionProtectionToAllRelationships

# Review the migration
# Then apply it:
dotnet ef database update
```

## FAQ

**Q: What happens if I delete without applying the migration?**
A: The error handling will still catch DbUpdateException, but the database won't enforce constraints at the DB level.

**Q: Should I use both custom exception AND database constraints?**
A: Yes! Database constraints provide the ultimate safety net, while the custom exception provides early detection with better messages.

**Q: How do I handle cascading deletes?**
A: For entities where cascading is desired (e.g., Document → DocumentBlob), use `OnDelete(DeleteBehavior.Cascade)` instead of Restrict.

**Q: Can I customize the error messages?**
A: Yes, modify the `ConvertTableNameToReadable()` method in DeletionErrorHandler.cs to customize dependency names.
