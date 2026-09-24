using FourierIT_API.Models;

namespace FourierIT_API.Services;

public sealed record DocumentValidityResult(DateTimeOffset ExpiryDate, bool MissingSourceDate);

public class DocumentValidityCalculator
{
    public static bool IsNeverExpires(DocumentType? documentType, DateTimeOffset expiryDate)
    {
        return documentType?.NeverExpires == true || expiryDate == DateTimeOffset.MaxValue;
    }

    public bool IsExpiringSoon(DocumentType? documentType, DateTimeOffset expiryDate, DateTimeOffset? asOf = null)
    {
        var now = asOf ?? DateTimeOffset.UtcNow;

        if (IsNeverExpires(documentType, expiryDate))
            return false;

        if (expiryDate <= now)
            return false;

        var warningDays = documentType?.WarningDays ?? 30;
        if (warningDays <= 0)
            return false;

        var daysRemaining = (int)Math.Floor((expiryDate - now).TotalDays);
        return daysRemaining <= warningDays;
    }

    public DocumentValidityResult Calculate(
        DocumentType documentType,
        DateTime uploadedDate,
        DateTimeOffset? certificationDate = null)
    {
        if (documentType.NeverExpires)
            return new DocumentValidityResult(DateTimeOffset.MaxValue, false);

        DateTimeOffset? basisDate = documentType.ValidityBasis switch
        {
            ValidityBasis.CertificationDate => certificationDate,
            ValidityBasis.UploadDate => new DateTimeOffset(uploadedDate),
            _ => null
        };

        if (basisDate is null)
            return new DocumentValidityResult(DateTimeOffset.MaxValue, true);

        return new DocumentValidityResult(basisDate.Value.AddMonths(documentType.ValidityMonths), false);
    }
}