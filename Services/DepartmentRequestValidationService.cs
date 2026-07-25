namespace FourierIT_API.Services;

public class DepartmentRequestValidationService
{
    public DepartmentRequestValidationResult ValidateRequestedDocumentTypes(
        IEnumerable<int> allowedDocumentTypeIds,
        IEnumerable<int> requestedDocumentTypeIds)
    {
        var allowed = (allowedDocumentTypeIds ?? Array.Empty<int>()).Distinct().OrderBy(id => id).ToList();
        var requested = (requestedDocumentTypeIds ?? Array.Empty<int>()).Distinct().OrderBy(id => id).ToList();

        var invalidDocumentTypeIds = requested
            .Where(id => !allowed.Contains(id))
            .ToArray();

        return new DepartmentRequestValidationResult(
            invalidDocumentTypeIds.Length == 0,
            invalidDocumentTypeIds,
            allowed);
    }
}

public sealed record DepartmentRequestValidationResult(
    bool IsValid,
    int[] InvalidDocumentTypeIds,
    IReadOnlyList<int> AllowedDocumentTypeIds);
