using System.Globalization;

namespace FourierIT_API.Services;

/// <summary>
/// South African ID numbers are 13 digits: YYMMDD (date of birth), SSSS (sequence/gender),
/// C (citizenship), A (usually 8) and Z (a Luhn check digit).
/// </summary>
public static class SouthAfricanIdNumber
{
    /// <summary>Entity type used for people identified by a South African ID number.</summary>
    public const int EntityTypeId = 1;

    /// <summary>
    /// Reads the date of birth from a valid ID number. Returns false when the number is not 13 digits,
    /// its first six digits are not a real date, or its check digit is wrong.
    /// </summary>
    public static bool TryGetDateOfBirth(string? idNumber, out DateOnly dateOfBirth, DateOnly? today = null)
    {
        dateOfBirth = default;
        var digits = (idNumber ?? string.Empty).Trim();
        if (digits.Length != 13 || !digits.All(char.IsAsciiDigit) || !HasValidCheckDigit(digits))
            return false;

        var now = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var yy = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var month = int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture);

        // Two-digit years: a year that would be in the future belongs to the previous century.
        var year = 2000 + yy;
        if (year > now.Year) year -= 100;

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        var candidate = new DateOnly(year, month, day);
        if (candidate > now)
            candidate = candidate.AddYears(-100);

        dateOfBirth = candidate;
        return true;
    }

    private static bool HasValidCheckDigit(string digits)
    {
        // South African ID numbers use the official check-digit algorithm:
        // weighted sum of the first 12 digits, then adjust modulo 11 and normalize 10/11 to 0.
        var weights = new[] { 8, 7, 6, 5, 4, 3, 2, 10, 0, 5, 4, 3 };
        var sum = 0;

        for (var i = 0; i < 12; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        var checkDigit = 11 - (sum % 11);
        if (checkDigit == 10 || checkDigit == 11)
        {
            checkDigit = 0;
        }

        return checkDigit == digits[12] - '0';
    }
}
