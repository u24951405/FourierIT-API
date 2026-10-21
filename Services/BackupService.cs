using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FourierIT_API.Data;
using FourierIT_API.DTOs;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;

namespace FourierIT_API.Services
{
    public class BackupService : IBackupService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IAuditLogService? _auditLogService;

        public BackupService(
            AppDbContext context,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<BackupService> logger,
            BlobServiceClient blobServiceClient,
            IAuditLogService? auditLogService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// The BACKUP statement for this server. SQL Server Express can't compress backups
        /// ("BACKUP DATABASE WITH COMPRESSION is not supported on Express Edition"), so it only asks for compression elsewhere.
        /// </summary>
        public static string BuildBackupSql(string databaseName, string filePath, bool supportsCompression) =>
            $"BACKUP DATABASE [{databaseName.Replace("]", "]]")}] TO DISK = '{filePath.Replace("'", "''")}' WITH FORMAT, INIT"
            + (supportsCompression ? ", COMPRESSION" : string.Empty)
            + ", MAXTRANSFERSIZE = 1048576, BUFFERCOUNT = 8;";

        /// <summary>Engine edition 4 is Express (see SERVERPROPERTY('EngineEdition')).</summary>
        private async Task<bool> ServerSupportsCompressionAsync()
        {
            try
            {
                var edition = await _context.Database
                    .SqlQueryRaw<int>("SELECT CAST(SERVERPROPERTY('EngineEdition') AS int) AS [Value]")
                    .SingleAsync();
                return edition != 4;
            }
            catch (Exception ex)
            {
                // If we can't tell, don't compress: an uncompressed backup works on every edition.
                _logger.LogWarning(ex, "Could not read the SQL Server edition; backing up without compression.");
                return false;
            }
        }

        // =========================================================================
        // 1. CREATE BACKUP
        // =========================================================================
        public async Task<BackupResponseDto> CreateDatabaseBackupAsync(CreateBackupRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var response = new BackupResponseDto
            {
                UserId = request.UserId ?? string.Empty,
                IsManualBackup = request.IsManualBackup,
                DateBackedUp = DateTimeOffset.UtcNow
            };

            string? containerName = _configuration["AzureBlobStorage:ContainerName"];
            if (string.IsNullOrWhiteSpace(containerName))
            {
                const string msg = "Azure Blob Storage container name is not configured.";
                _logger.LogError(msg);
                response.StatusMessage = msg;
                return response;
            }

            string? connectionString = _context.Database.GetDbConnection().ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                const string msg = "Database connection string is not available.";
                _logger.LogError(msg);
                response.StatusMessage = msg;
                return response;
            }

            string dbName;
            try
            {
                dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName))
                    throw new InvalidOperationException("Unable to determine database name from connection string.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse database connection string.");
                response.StatusMessage = "Failed to determine database name.";
                return response;
            }

            var supportsCompression = await ServerSupportsCompressionAsync();
            string fileName = $"{dbName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.bak";
            response.FileName = fileName;

            // The file must be written by SQL Server's Windows account and then read (and deleted) by the app's account.
            // SQL Server's own backup folder is usually closed to the app, and the app's folder (inside OneDrive here) is
            // usually closed to SQL Server, so a folder both can use is set in Backup:LocalFolder. The app's folder is the fallback.
            var folders = new List<string>();
            var configuredFolder = _configuration["Backup:LocalFolder"];
            if (!string.IsNullOrWhiteSpace(configuredFolder))
            {
                try { Directory.CreateDirectory(configuredFolder); folders.Add(configuredFolder); }
                catch (Exception ex) { _logger.LogWarning(ex, "Backup:LocalFolder {Folder} can't be used.", configuredFolder); }
            }
            var appFolder = Path.Combine(_env.ContentRootPath, "TempBackups");
            Directory.CreateDirectory(appFolder);
            folders.Add(appFolder);

            Exception? lastError = null;
            foreach (var folder in folders)
            {
                var filePath = Path.Combine(folder, fileName);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    _logger.LogInformation("Backing up to {FilePath} (compression: {Compression})", filePath, supportsCompression);
                    _context.Database.SetCommandTimeout(3600); // backups can take a while
                    await _context.Database.ExecuteSqlRawAsync(BuildBackupSql(dbName, filePath, supportsCompression));
                    response.BackupSeconds = Math.Round(timer.Elapsed.TotalSeconds, 1);
                    response.SizeBytes = new FileInfo(filePath).Length;
                }
                catch (Exception ex) when (IsFolderAccessProblem(ex))
                {
                    _logger.LogWarning(ex, "SQL Server could not write to {Folder}; trying the next folder.", folder);
                    lastError = ex;
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The database backup failed.");
                    response.StatusMessage = "The backup failed: " + ex.Message;
                    DeleteQuietly(filePath);
                    return response;
                }

                try
                {
                    timer.Restart();
                    var result = await UploadAndRecordAsync(request, response, containerName, fileName, filePath);
                    response.UploadSeconds = Math.Round(timer.Elapsed.TotalSeconds, 1);
                    response.StatusMessage =
                        $"Backup created and uploaded ({FormatSize(response.SizeBytes)}: backed up in {response.BackupSeconds:0.#}s, uploaded in {response.UploadSeconds:0.#}s).";
                    _logger.LogInformation("{Status}", response.StatusMessage);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The backup was made but could not be uploaded to Azure Blob Storage.");
                    response.StatusMessage = "The backup was made but could not be uploaded: " + ex.Message;
                    return response;
                }
                finally
                {
                    DeleteQuietly(filePath);
                }
            }

            response.StatusMessage = "The backup failed: SQL Server could not write the backup file to any folder. " + lastError?.Message;
            return response;
        }

        /// <summary>SQL Server couldn't create the file there (folder permissions), as opposed to the backup itself failing.</summary>
        private static bool IsFolderAccessProblem(Exception ex) =>
            ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Operating system error", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Cannot open backup device", StringComparison.OrdinalIgnoreCase);

        private void DeleteQuietly(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete the local backup file {Path}", path); }
        }

        private static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

        /// <summary>
        /// Uploads in 4 MB blocks, eight at a time; the defaults send much smaller pieces one after another,
        /// which is slow on a home connection.
        /// </summary>
        private static readonly Azure.Storage.StorageTransferOptions UploadTransfer = new()
        {
            InitialTransferSize = 4 * 1024 * 1024,
            MaximumTransferSize = 4 * 1024 * 1024,
            MaximumConcurrency = 8
        };

        private async Task<BackupResponseDto> UploadAndRecordAsync(CreateBackupRequestDto request, BackupResponseDto response,
            string containerName, string fileName, string filePath)
        {
            _logger.LogInformation("Uploading backup to Azure Blob Storage container '{Container}'", containerName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            using (var fileStream = File.OpenRead(filePath))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                await blobClient.UploadAsync(fileStream, new Azure.Storage.Blobs.Models.BlobUploadOptions { TransferOptions = UploadTransfer }, cts.Token);
            }

            var backup = new Backup
            {
                UserId = await ExistingUserIdOrNullAsync(request.UserId),
                FileName = fileName,
                DateBackedUp = DateTimeOffset.UtcNow,
                IsManualBackup = request.IsManualBackup
            };
            _context.Backups.Add(backup);
            await _context.SaveChangesAsync();

            response.Success = true;
            response.BackupId = backup.BackupId;
            response.FilePath = blobClient.Uri.ToString();
            response.DateBackedUp = backup.DateBackedUp;
            _logger.LogInformation("Backup (Id: {BackupId}) uploaded to {BlobUrl}", backup.BackupId, response.FilePath);
            return response;
        }

        private async Task<string?> ExistingUserIdOrNullAsync(string? userId) =>
            !string.IsNullOrWhiteSpace(userId) && await _context.Users.AnyAsync(u => u.Id == userId) ? userId : null;

        // =========================================================================
        // 2. GET ALL BACKUPS
        // =========================================================================
        public async Task<IEnumerable<Backup>> GetAllBackupsAsync()
        {
            return await _context.Backups.AsNoTracking().OrderByDescending(b => b.DateBackedUp).ToListAsync();
        }

        /// <summary>
        /// The backup list lives in the database being restored, so a restore would forget every backup made after the one chosen.
        /// This puts back the records that are missing afterwards (the files are still in Azure).
        /// </summary>
        public static List<Backup> MissingAfterRestore(IEnumerable<Backup> before, IEnumerable<string> fileNamesAfter)
        {
            var present = new HashSet<string>(fileNamesAfter, StringComparer.OrdinalIgnoreCase);
            return before
                .Where(b => !present.Contains(b.FileName))
                .Select(b => new Backup { FileName = b.FileName, DateBackedUp = b.DateBackedUp, IsManualBackup = b.IsManualBackup, UserId = b.UserId })
                .ToList();
        }

        // =========================================================================
        // 3. RESTORE DATABASE
        // =========================================================================
        public async Task<RestoreResponseDto> RestoreDatabaseAsync(int backupId, string? restoredByUserId = null)
        {
            string? tempFilePath = null;
            try
            {
                _logger.LogInformation("Starting database restore for BackupId={BackupId}", backupId);

                var backup = await _context.Backups.AsNoTracking().FirstOrDefaultAsync(b => b.BackupId == backupId);
                if (backup == null)
                {
                    _logger.LogWarning("Backup record not found for id {BackupId}", backupId);
                    return new RestoreResponseDto { Success = false, Message = $"Backup record with id {backupId} not found.", RestoredAt = DateTimeOffset.UtcNow };
                }

                // Remember the full backup list before the restore replaces it.
                var backupsBefore = await _context.Backups.AsNoTracking().ToListAsync();

                string containerName = _configuration["AzureBlobStorage:ContainerName"] ?? "docuvault-database-backups";
                var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(backup.FileName);

                var tempDir = Path.Combine(_env.ContentRootPath, "TempBackups");
                Directory.CreateDirectory(tempDir);
                tempFilePath = Path.Combine(tempDir, Path.GetFileName(backup.FileName));

                _logger.LogInformation("Downloading backup blob {FileName} to {TempFilePath}", backup.FileName, tempFilePath);
                using (var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    await blobClient.DownloadToAsync(tempFilePath, downloadCts.Token);
                }

                var currentConnectionString = _context.Database.GetDbConnection().ConnectionString;
                var targetDbName = string.IsNullOrWhiteSpace(currentConnectionString) ? null : new SqlConnectionStringBuilder(currentConnectionString).InitialCatalog;
                if (string.IsNullOrWhiteSpace(targetDbName))
                {
                    _logger.LogError("Could not determine target database name from connection string.");
                    return new RestoreResponseDto { Success = false, Message = "Could not determine the database to restore.", RestoredAt = DateTimeOffset.UtcNow };
                }

                // Release this request's own connection before taking the database offline.
                await _context.Database.CloseConnectionAsync();

                var masterConnectionString = new SqlConnectionStringBuilder(currentConnectionString) { InitialCatalog = "master" }.ConnectionString;
                var quotedDb = targetDbName.Replace("]", "]]");
                using (var conn = new SqlConnection(masterConnectionString))
                {
                    await conn.OpenAsync();
                    await ExecuteAsync(conn, $"ALTER DATABASE [{quotedDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
                    try
                    {
                        await ExecuteAsync(conn, $"RESTORE DATABASE [{quotedDb}] FROM DISK = '{tempFilePath.Replace("'", "''")}' WITH REPLACE;");
                    }
                    finally
                    {
                        await ExecuteAsync(conn, $"ALTER DATABASE [{quotedDb}] SET MULTI_USER;");
                    }
                }

                // Every pooled connection was cut when the database went single-user; drop them so the next requests
                // open fresh ones instead of failing.
                SqlConnection.ClearAllPools();
                _context.ChangeTracker.Clear();

                // An older backup can predate recent changes to the database; bring it up to what this version of the app expects.
                var pending = (await _context.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0)
                {
                    _logger.LogInformation("Applying {Count} migrations to the restored database: {Migrations}", pending.Count, string.Join(", ", pending));
                    await _context.Database.MigrateAsync();
                }

                // Put back the backups made after the one restored, so they can still be chosen later.
                var fileNamesAfter = await _context.Backups.AsNoTracking().Select(b => b.FileName).ToListAsync();
                var missing = MissingAfterRestore(backupsBefore, fileNamesAfter);
                foreach (var record in missing)
                {
                    record.UserId = await ExistingUserIdOrNullAsync(record.UserId);
                    _context.Backups.Add(record);
                }
                if (missing.Count > 0) await _context.SaveChangesAsync();

                await RecordRestoreAsync(backup, restoredByUserId);

                _logger.LogInformation("Database restore completed successfully for backup id {BackupId}", backupId);
                return new RestoreResponseDto
                {
                    Success = true,
                    Message = $"The database was restored to the backup of {backup.DateBackedUp.ToOffset(TimeSpan.FromHours(2)):d MMM yyyy, HH:mm} (SAST)."
                        + (pending.Count > 0 ? $" {pending.Count} database update{(pending.Count == 1 ? " was" : "s were")} applied so it matches this version of DocuVault." : string.Empty),
                    RestoredAt = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database restore for backup id {BackupId}", backupId);
                SqlConnection.ClearAllPools();
                return new RestoreResponseDto { Success = false, Message = $"Error during restore: {ex.Message}", RestoredAt = DateTimeOffset.UtcNow };
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete temporary backup file {TempFilePath}", tempFilePath);
                }
            }
        }

        private async Task ExecuteAsync(SqlConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 3600;
            cmd.CommandText = sql;
            _logger.LogInformation("Executing: {Sql}", sql);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Written after the restore, into the restored database's audit trail, so the record of the restore itself survives.
        /// </summary>
        private async Task RecordRestoreAsync(Backup backup, string? restoredByUserId)
        {
            if (_auditLogService == null) return;
            try
            {
                await _auditLogService.CreateAuditLogAsync(new AuditLog
                {
                    UserId = await ExistingUserIdOrNullAsync(restoredByUserId),
                    ActionCode = "DATABASE_RESTORED",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = $"Database restored from backup \"{backup.FileName}\" (made {backup.DateBackedUp:yyyy-MM-dd HH:mm} UTC). Changes made after that backup were replaced.",
                    TableAffected = "Backups",
                    RecordID = backup.BackupId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The restore worked, but it could not be written to the audit log.");
            }
        }
    }
}
