using FourierIT_API.Services;

namespace FourierIT.API.Tests;

public class DepartmentRequestValidationServiceTests
{
    [Fact]
    public void ValidateRequestedDocumentTypes_ReturnsOnlyAllowedDepartmentDocumentTypes()
    {
        var service = new DepartmentRequestValidationService();

        var allowedIds = new[] { 1, 2, 4, 7 };
        var requestedIds = new[] { 1, 3, 4, 9 };

        var result = service.ValidateRequestedDocumentTypes(allowedIds, requestedIds);

        Assert.False(result.IsValid);
        Assert.Equal(new[] { 3, 9 }, result.InvalidDocumentTypeIds);
        Assert.Equal(new[] { 1, 2, 4, 7 }, result.AllowedDocumentTypeIds);
    }
}
