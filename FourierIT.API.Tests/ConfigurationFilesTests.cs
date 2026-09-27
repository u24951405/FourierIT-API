using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FourierIT.API.Tests;

public class ConfigurationFilesTests
{
    [Fact]
    public void AppSettingsJson_ContainsValidSmtpEmailConfiguration()
    {
        var settingsPath = Path.GetFullPath(Path.Combine(ThisFileDirectory(), "..", "appsettings.json"));

        Assert.True(File.Exists(settingsPath), $"Expected settings file at {settingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var emailSettings = document.RootElement.GetProperty("EmailSettings");

        Assert.Equal("smtp.gmail.com", emailSettings.GetProperty("Host").GetString());
        Assert.True(emailSettings.GetProperty("Port").GetInt32() > 0);
        Assert.Equal("DocuVault", emailSettings.GetProperty("FromDisplayName").GetString());
        // Gmail only sends as the signed-in account, so the sender must be the SMTP username.
        Assert.Equal(emailSettings.GetProperty("Username").GetString(), emailSettings.GetProperty("FromAddress").GetString());
        // The app password must come from User Secrets or an environment variable, never this file.
        Assert.True(string.IsNullOrEmpty(emailSettings.GetProperty("Password").GetString()));
    }

    [Fact]
    public void DevelopmentSettings_DoNotAttemptDatabaseInitializationByDefault()
    {
        var settingsPath = Path.GetFullPath(Path.Combine(ThisFileDirectory(), "..", "appsettings.Development.json"));

        Assert.True(File.Exists(settingsPath), $"Expected settings file at {settingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var startupTasks = document.RootElement.GetProperty("StartupTasks");

        Assert.False(startupTasks.GetProperty("RunDatabaseInitialization").GetBoolean());
        Assert.False(startupTasks.GetProperty("RunDevSeed").GetBoolean());
    }

    // The test project sits inside the API folder, so appsettings.json is one level up from this file,
    // however the tests are built (bin folder, a custom output path, or CI).
    private static string ThisFileDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
