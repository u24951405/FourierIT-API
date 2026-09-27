using FourierIT_API.Models;

namespace FourierIT_API.Services;

/// <param name="MissingSourceDate">
/// True when the type counts from the certification date but the document has none, so the upload date was used.
/// </param>
public sealed record DocumentValidityResult(DateTimeOffset ExpiryDate, bool MissingSourceDate);

/// <summary>
/// Every document is valid for its type's number of months, counted from either the date it was certified
/// or the date it was uploaded (the type's validity basis). There are no documents that never expire.
/// </summary>
public class DocumentValidityCalculator
{
    public bool IsExpiringSoon(DocumentType? documentType, DateTimeOffset expiryDate, DateTimeOffset? asOf = null)
    {
        var now = asOf ?? DateTimeOffset.UtcNow;

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
        var uploaded = new DateTimeOffset(DateTime.SpecifyKind(uploadedDate, uploadedDate.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : uploadedDate.Kind));
        var missingCertificationDate = documentType.ValidityBasis == ValidityBasis.CertificationDate && certificationDate is null;

        // Counted from the certification date when the type says so; a document without one (e.g. uploaded
        // before the date was required) is counted from its upload date, so it still expires.
        var basisDate = documentType.ValidityBasis == ValidityBasis.CertificationDate && certificationDate is not null
            ? certificationDate.Value
            : uploaded;

        var months = documentType.ValidityMonths > 0 ? documentType.ValidityMonths : 1;
        return new DocumentValidityResult(basisDate.AddMonths(months), missingCertificationDate);
    }
}
