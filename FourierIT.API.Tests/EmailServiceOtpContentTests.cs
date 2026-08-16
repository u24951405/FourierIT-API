using System.Reflection;
using FourierIT_API.Services;
using Xunit;

namespace FourierIT.API.Tests;

public class EmailServiceOtpContentTests
{
    [Fact]
    public void UserRegistrationOtpEmail_UsesUserRegistrationContext()
    {
        var htmlBody = InvokeStaticMethod("BuildUserRegistrationOtpEmailHtmlBody", "123456", DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.Contains("Welcome to DocuVault", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify your email to complete registration", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123456", htmlBody);
        Assert.DoesNotContain("institution portal access code", htmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstitutionOtpEmail_UsesInstitutionPortalContext()
    {
        var htmlBody = InvokeStaticMethod("BuildInstitutionOtpEmailHtmlBody", "Example Institution", "654321", DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.Contains("Your institution portal access code", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Example Institution", htmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("654321", htmlBody);
        Assert.DoesNotContain("Welcome to DocuVault", htmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private static string InvokeStaticMethod(string methodName, params object[] args)
    {
        var method = typeof(EmailService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, args);
        Assert.NotNull(result);
        return result!.ToString()!;
    }
}
