using FourierIT_API.Models;

namespace FourierIT_API.Services;

public sealed record DocumentValidityResult(DateTimeOffset ExpiryDate, bool MissingSourceDate);

public class DocumentValidityCalculator
{
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