using FourierIT_API.Models;
using FourierIT_API.Services;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>The parts of backup and restore that don't need a real SQL Server or Azure.</summary>
public class BackupServiceRulesTests
{
    [Fact]
    public void BackupSql_OnlyAsksForCompressionWhenTheServerSupportsIt()
    {
        // SQL Server Express rejects WITH COMPRESSION, so every backup failed there.
        var express = BackupService.BuildBackupSql("FourierIT", @"C:\Temp\FourierIT.bak", supportsCompression: false);
        var standard = BackupService.BuildBackupSql("FourierIT", @"C:\Temp\FourierIT.bak", supportsCompression: true);

        Assert.DoesNotContain("COMPRESSION", express);
        Assert.Contains("COMPRESSION", standard);
        Assert.StartsWith("BACKUP DATABASE [FourierIT] TO DISK = 'C:\\Temp\\FourierIT.bak'", express);
    }

    [Fact]
    public void BackupSql_EscapesQuotesInThePath()
    {
        var sql = BackupService.BuildBackupSql("FourierIT", @"C:\O'Brien\db.bak", supportsCompression: false);

        Assert.Contains(@"'C:\O''Brien\db.bak'", sql);
    }

    [Fact]
    public void Restore_PutsBackTheBackupsMadeAfterTheOneRestored()
    {
        var monday = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
        var before = new[]
        {
            new Backup { BackupId = 1, FileName = "FourierIT_mon.bak", DateBackedUp = monday, IsManualBackup = false },
            new Backup { BackupId = 2, FileName = "FourierIT_tue.bak", DateBackedUp = monday.AddDays(1), IsManualBackup = true, UserId = "admin" },
            new Backup { BackupId = 3, FileName = "FourierIT_wed.bak", DateBackedUp = monday.AddDays(2), IsManualBackup = false },
        };

        // Restoring Monday's backup leaves only Monday's record in the restored database.
        var missing = BackupService.MissingAfterRestore(before, new[] { "FourierIT_mon.bak" });

        Assert.Equal(new[] { "FourierIT_tue.bak", "FourierIT_wed.bak" }, missing.Select(b => b.FileName));
        Assert.All(missing, b => Assert.Equal(0, b.BackupId)); // new rows in the restored database
        Assert.Equal("admin", missing[0].UserId);
        Assert.True(missing[0].IsManualBackup);
    }
}
