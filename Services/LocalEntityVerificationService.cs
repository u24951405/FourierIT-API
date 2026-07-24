using FourierIT_API.Interfaces;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace FourierIT_API.Services
{
    public class LocalEntityVerificationService : IEntityVerificationService
    {
        public Task<EntityVerificationResult> VerifyEntityAsync(int entityTypeId, string identificationNumber)
        {
            var normalizedIdentificationNumber = identificationNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedIdentificationNumber))
            {
                return Task.FromResult(EntityVerificationResult.Invalid("Identification number is required.", false));
            }

            var validationError = ValidateEntityIdentificationNumber(entityTypeId, normalizedIdentificationNumber);
            if (validationError != null)
            {
                return Task.FromResult(EntityVerificationResult.Invalid(validationError, false));
            }

            return Task.FromResult(EntityVerificationResult.Valid(false));
        }

        private static string? ValidateEntityIdentificationNumber(int entityTypeId, string verificationNumber)
        {
            switch (entityTypeId)
            {
                case 1:
                    return ValidateSouthAfricanId(verificationNumber);
                case 2:
                    return ValidatePassportNumber(verificationNumber);
                case 3:
                    return ValidateCompanyRegistrationNumber(verificationNumber);
                case 4:
                    return ValidateTrustRegistrationNumber(verificationNumber);
                case 5:
                    return ValidatePartnershipRegistrationNumber(verificationNumber);
                default:
                    return ValidateOtherEntityNumber(verificationNumber);
            }
        }

        private static string? ValidateSouthAfricanId(string idNumber)
        {
            if (idNumber.Length != 13 || !idNumber.All(char.IsDigit))
                return "South African ID numbers must be exactly 13 digits.";

            if (!DateTime.TryParseExact(idNumber.Substring(0, 6), "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "South African ID numbers must contain a valid date of birth.";

            var digits = idNumber.Select(c => c - '0').ToArray();
            var weights = new[] { 8, 7, 6, 5, 4, 3, 2, 10, 0, 5, 4, 3 };
            var sum = 0;

            for (var i = 0; i < 12; i++)
            {
                sum += digits[i] * weights[i];
            }

            var checkDigit = 11 - (sum % 11);
            if (checkDigit == 10 || checkDigit == 11)
            {
                checkDigit = 0;
            }

            return checkDigit == digits[12]
                ? null
                : "South African ID numbers must have a valid checksum.";
        }

        private static string? ValidatePassportNumber(string passportNumber)
        {
            if (passportNumber.Length < 4 || passportNumber.Length > 20)
                return "Passport numbers must be between 4 and 20 characters.";
            if (!passportNumber.All(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '))
                return "Passport numbers may only contain letters, numbers, spaces, and hyphens.";
            return null;
        }

        private static string? ValidateCompanyRegistrationNumber(string registrationNumber)
        {
            if (registrationNumber.Length < 4 || registrationNumber.Length > 20)
                return "Company registration numbers must be between 4 and 20 characters.";
            if (!registrationNumber.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '/'))
                return "Company registration numbers may only contain letters, numbers, hyphens, and slashes.";
            return null;
        }

        private static string? ValidateTrustRegistrationNumber(string registrationNumber)
        {
            if (registrationNumber.Length < 4 || registrationNumber.Length > 20)
                return "Trust registration numbers must be between 4 and 20 characters.";
            if (!registrationNumber.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '/'))
                return "Trust registration numbers may only contain letters, numbers, hyphens, and slashes.";
            return null;
        }

        private static string? ValidatePartnershipRegistrationNumber(string registrationNumber)
        {
            if (registrationNumber.Length < 4 || registrationNumber.Length > 20)
                return "Partnership registration numbers must be between 4 and 20 characters.";
            if (!registrationNumber.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '/'))
                return "Partnership registration numbers may only contain letters, numbers, hyphens, and slashes.";
            return null;
        }

        private static string? ValidateOtherEntityNumber(string registrationNumber)
        {
            if (registrationNumber.Length < 4 || registrationNumber.Length > 40)
                return "Reference numbers must be between 4 and 40 characters.";
            return null;
        }
    }
}
