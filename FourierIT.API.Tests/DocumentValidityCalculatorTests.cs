using FourierIT_API.Models;
using FourierIT_API.Services;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentValidityCalculatorTests
{
    private readonly DocumentValidityCalculator _calculator = new();

    [Fact]
    public void CertificationBasisWithoutCertificationDate_CountsFromTheUploadDate()
    {
        // Every document expires: without a certification date the upload date is the starting point.
        var policy = new DocumentType { ValidityMonths = 3, ValidityBasis = ValidityBasis.CertificationDate };

        var result = _calculator.Calculate(policy, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), result.ExpiryDate);
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
    public void ExistingDocument_RecalculatesWhenCertificationDateChanges()
    {
        var policy = new DocumentType { ValidityBasis = ValidityBasis.CertificationDate };
        var document = new Document
        {
            UploadedDate = new DateTime(2026, 1, 1),
            ExpiryDate = DateTimeOffset.MaxValue
        };
        var initialCertificationDate = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var changedCertificationDate = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

        document.ExpiryDate = _calculator.Calculate(
            policy,
            document.UploadedDate,
            initialCertificationDate).ExpiryDate;
        document.ExpiryDate = _calculator.Calculate(
            policy,
            document.UploadedDate,
            changedCertificationDate).ExpiryDate;

        Assert.Equal(changedCertificationDate.AddMonths(3), document.ExpiryDate);
    }

    [Fact]
    public void DefaultSettings_WithCertificationDate_ExpireAfterThreeMonths()
    {
        var policy = new DocumentType();
        var certificationDate = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var result = _calculator.Calculate(policy, new DateTime(2026, 1, 1), certificationDate);

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

    [Fact]
    public void IsExpiringSoon_ExpiredDocument_IsFalse()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var policy = new DocumentType { WarningDays = 30 };

        Assert.False(_calculator.IsExpiringSoon(policy, now.AddDays(-1), now));
    }

    [Fact]
    public void IsExpiringSoon_WithinWarningDays_IsTrue()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var policy = new DocumentType { WarningDays = 30 };

        Assert.True(_calculator.IsExpiringSoon(policy, now.AddDays(15), now));
    }

    [Fact]
    public void IsExpiringSoon_OutsideWarningDays_IsFalse()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var policy = new DocumentType { WarningDays = 30 };

        Assert.False(_calculator.IsExpiringSoon(policy, now.AddDays(45), now));
    }

    [Fact]
    public void IsExpiringSoon_WarningDaysZero_IsFalse()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var policy = new DocumentType { WarningDays = 0 };

        Assert.False(_calculator.IsExpiringSoon(policy, now.AddDays(5), now));
    }

    [Fact]
    public void FutureExpiry_WithOldUploadedDate_IsStillCurrent()
    {
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            UploadedDate = now.AddYears(-5).UtcDateTime,
            ExpiryDate = now.AddDays(60)
        };

        Assert.True(document.ExpiryDate > now);
        Assert.False(_calculator.IsExpiringSoon(new DocumentType { WarningDays = 30 }, document.ExpiryDate, now));
    }
}