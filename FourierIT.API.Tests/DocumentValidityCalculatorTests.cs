using FourierIT_API.Models;
using FourierIT_API.Services;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentValidityCalculatorTests
{
    private readonly DocumentValidityCalculator _calculator = new();

    [Fact]
    public void NeverExpires_NeverExpires()
    {
        var policy = new DocumentType
        {
            NeverExpires = true,
            ValidityMonths = 3,
            ValidityBasis = ValidityBasis.CertificationDate
        };

        var result = _calculator.Calculate(policy, new DateTime(2026, 1, 1));

        Assert.Equal(DateTimeOffset.MaxValue, result.ExpiryDate);
        Assert.False(result.MissingSourceDate);
    }

    [Fact]
    public void CertificationBasisWithoutCertificationDate_ReportsMissingSourceDate()
    {
        var policy = new DocumentType { ValidityMonths = 3, ValidityBasis = ValidityBasis.CertificationDate };

        var result = _calculator.Calculate(policy, new DateTime(2026, 1, 1));

        Assert.Equal(DateTimeOffset.MaxValue, result.ExpiryDate);
        Assert.True(result.MissingSourceDate);
    }

    [Fact]
    public void CertificationBasis_UsesCertificationDate()
    {
        var policy = new DocumentType { ValidityMonths = 3, ValidityBasis = ValidityBasis.CertificationDate };
        var certificationDate = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var result = _calculator.Calculate(policy, new DateTime(2026, 2, 1), certificationDate);

        Assert.Equal(certificationDate.AddMonths(3), result.ExpiryDate);
        Assert.False(result.MissingSourceDate);
    }

    [Fact]
    public void UploadBasis_UsesUploadedDate()
    {
        var policy = new DocumentType { ValidityMonths = 3, ValidityBasis = ValidityBasis.UploadDate };
        var uploadedDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = _calculator.Calculate(policy, uploadedDate);

        Assert.Equal(new DateTimeOffset(uploadedDate).AddMonths(3), result.ExpiryDate);
        Assert.False(result.MissingSourceDate);
    }

    [Fact]
    public void AddMonths_HandlesEndOfMonth()
    {
        var policy = new DocumentType { ValidityMonths = 1, ValidityBasis = ValidityBasis.CertificationDate };
        var certificationDate = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var result = _calculator.Calculate(policy, new DateTime(2026, 1, 1), certificationDate);

        Assert.Equal(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), result.ExpiryDate);
    }
}