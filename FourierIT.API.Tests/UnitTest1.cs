using FourierIT_API.Services;

namespace FourierIT.API.Tests;

public class EntityVerificationServiceTests
{
    [Fact]
    public async Task LocalEntityVerificationService_ValidSouthAfricanId_ReturnsValid()
    {
        var service = new LocalEntityVerificationService();

        var result = await service.VerifyEntityAsync(1, "9001015009061");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task LocalEntityVerificationService_ValidSouthAfricanIdWithWhitespace_ReturnsValid()
    {
        var service = new LocalEntityVerificationService();

        var result = await service.VerifyEntityAsync(1, " 9001015009061 ");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task LocalEntityVerificationService_InvalidSouthAfricanId_ReturnsInvalid()
    {
        var service = new LocalEntityVerificationService();

        var result = await service.VerifyEntityAsync(1, "9001015009087");

        Assert.False(result.IsValid);
        Assert.Equal("South African ID numbers must have a valid checksum.", result.ErrorMessage);
    }
}
