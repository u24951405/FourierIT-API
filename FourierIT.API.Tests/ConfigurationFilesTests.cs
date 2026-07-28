using System.Text.Json;

namespace FourierIT.API.Tests;

public class ConfigurationFilesTests
{
    [Fact]
    public void AppSettingsJson_ContainsValidSmtpEmailConfiguration()
    {
        var settingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../appsettings.json"));

        Assert.True(File.Exists(settingsPath), $"Expected settings file at {settingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var emailSettings = document.RootElement.GetProperty("EmailSettings");

        Assert.Equal("smtp.sendgrid.net", emailSettings.GetProperty("Host").GetString());
        Assert.True(emailSettings.GetProperty("Port").GetInt32() > 0);
        Assert.Equal("DocuVault", emailSettings.GetProperty("FromDisplayName").GetString());
    }
}
