using FourierIT_API.Services;

namespace FourierIT.API.Tests;

public class SouthAfricanIdNumberTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    /// <summary>Builds a made-up ID number (not a real person's) with a correct Luhn check digit.</summary>
    private static string MakeId(string firstTwelveDigits)
    {
        for (var check = 0; check <= 9; check++)
        {
            var candidate = firstTwelveDigits + check;
            if (SouthAfricanIdNumber.TryGetDateOfBirth(candidate, out _, Today) || !char.IsDigit(candidate[^1]))
                return candidate;
        }
        throw new InvalidOperationException("No check digit produced a valid number.");
    }

    [Theory]
    [InlineData("850317", 1985, 3, 17)]
    [InlineData("010405", 2001, 4, 5)]
    [InlineData("260926", 2026, 9, 26)]
    [InlineData("270101", 1927, 1, 1)] // 2027 would be in the future, so it is 1927
    public void TryGetDateOfBirth_ReadsTheDateFromTheFirstSixDigits(string yymmdd, int year, int month, int day)
    {
        var id = MakeId(yymmdd + "500908");

        Assert.True(SouthAfricanIdNumber.TryGetDateOfBirth(id, out var dateOfBirth, Today));
        Assert.Equal(new DateOnly(year, month, day), dateOfBirth);
    }

    [Fact]
    public void TryGetDateOfBirth_AcceptsTheOfficialSouthAfricanChecksumAlgorithm()
    {
        const string validId = "9001015009061";

        Assert.True(SouthAfricanIdNumber.TryGetDateOfBirth(validId, out var dateOfBirth, Today));
        Assert.Equal(new DateOnly(1990, 1, 1), dateOfBirth);
    }

    [Fact]
    public void TryGetDateOfBirth_RejectsAWrongCheckDigit()
    {
        var valid = MakeId("850317500908");
        var wrongCheckDigit = valid[..12] + ((valid[12] - '0' + 1) % 10);

        Assert.False(SouthAfricanIdNumber.TryGetDateOfBirth(wrongCheckDigit, out _, Today));
    }

    [Theory]
    [InlineData("851317500908")] // month 13
    [InlineData("850230500908")] // 30 February
    [InlineData("12345")]         // too short
    [InlineData("85031750090A")] // not all digits
    public void TryGetDateOfBirth_RejectsNumbersThatAreNotValidIds(string value)
    {
        // Try every possible final digit so the check digit can't be what makes it valid.
        for (var check = 0; check <= 9; check++)
            Assert.False(SouthAfricanIdNumber.TryGetDateOfBirth(value + check, out _, Today));
    }
}
