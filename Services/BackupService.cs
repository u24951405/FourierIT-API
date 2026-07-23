using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
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

        public BackupService(
            AppDbContext context,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<BackupService> logger,
            BlobServiceClient blobServiceClient)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        }

        // =========================================================================
        // 1. CREATE BACKUP
        // =========================================================================
        public async Task<BackupResponseDto> CreateDatabaseBackupAsync(CreateBackupRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var response = new BackupResponseDto
            {
                UserId = request.UserId,
                IsManualBackup = request.IsManualBackup,
                DateBackedUp = DateTimeOffset.UtcNow
            };

            string containerName = _configuration["AzureBlobStorage:ContainerName"];
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
                var builder = new SqlConnectionStringBuilder(connectionString);
                dbName = builder.InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName))
                {
                    throw new InvalidOperationException("Unable to determine database name from connection string.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse database connection string.");
                response.StatusMessage = "Failed to determine database name.";
                return response;
            }

            string tempPath = Path.Combine(_env.ContentRootPath, "TempBackups");
            Directory.CreateDirectory(tempPath);

            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{dbName}_{timestamp}.bak";
            string tempFilePath = Path.Combine(tempPath, fileName);

            response.FileName = fileName;

            try
            {
                _logger.LogInformation("Starting database backup to local file {FilePath}", tempFilePath);

                string sql = $"BACKUP DATABASE [{dbName}] TO DISK = '{tempFilePath.Replace("'", "''")}' WITH FORMAT, INIT;";
                await _context.Database.ExecuteSqlRawAsync(sql);

                _logger.LogInformation("Database backup finished, uploading to Azure Blob Storage container '{Container}'", containerName);

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                var blobClient = containerClient.GetBlobClient(fileName);

                using (var fileStream = File.OpenRead(tempFilePath))
                {
                    await blobClient.UploadAsync(fileStream, overwrite: true);
                }

                string blobUrl = blobClient.Uri.ToString();

                string? validUserId = null;
                if (!string.IsNullOrWhiteSpace(request.UserId))
                {
                    bool userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
                    if (userExists)
                    {
                        validUserId = request.UserId;
                    }
                }

                var backup = new Backup
                {
                    UserId = validUserId,
                    FileName = fileName,
                    DateBackedUp = DateTimeOffset.UtcNow,
                    IsManualBackup = request.IsManualBackup
                };

                _context.Backups.Add(backup);
                await _context.SaveChangesAsync();

                response.BackupId = backup.BackupId;
                response.FilePath = blobUrl;
                response.DateBackedUp = backup.DateBackedUp;
                response.StatusMessage = "Backup created and uploaded successfully.";

                _logger.LogInformation("Backup (Id: {BackupId}) uploaded to {BlobUrl}", backup.BackupId, blobUrl);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating or uploading database backup.");
                response.StatusMessage = "An error occurred during backup: " + ex.Message;
                return response;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInformation("Deleted temporary backup file {TempFile}", tempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary backup file {TempFile}", tempFilePath);
                }
            }
        }

        // =========================================================================
        // 2. GET ALL BACKUPS
        // =========================================================================
        public async Task<IEnumerable<Backup>> GetAllBackupsAsync()
        {
            return await _context.Backups.OrderByDescending(b => b.DateBackedUp).ToListAsync();
        }

        // =========================================================================
        // 3. RESTORE DATABASE
        // =========================================================================
        public async Task<RestoreResponseDto> RestoreDatabaseAsync(int backupId)
        {
            string tempFilePath = null;
            try
            {
                _logger.LogInformation("Starting database restore for BackupId={BackupId}", backupId);

                var backup = await _context.Backups.FindAsync(backupId);
                if (backup == null)
                {
                    _logger.LogWarning("Backup record not found for id {BackupId}", backupId);
                    return new RestoreResponseDto
                    {
                        Success = false,
                        Message = $"Backup record with id {backupId} not found.",
                        RestoredAt = DateTimeOffset.UtcNow
                    };
                }

                string containerName = _configuration["AzureBlobStorage:ContainerName"] ?? "docuvault-database-backups";
                var container = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = container.GetBlobClient(backup.FileName);

                var tempDir = Path.Combine(_env.ContentRootPath, "TempBackups");
                Directory.CreateDirectory(tempDir);

                tempFilePath = Path.Combine(tempDir, backup.FileName);

                _logger.LogInformation("Downloading backup blob {FileName} to {TempFilePath}", backup.FileName, tempFilePath);
                await blobClient.DownloadToAsync(tempFilePath);
                _logger.LogInformation("Download complete for backup id {BackupId}", backupId);

                var currentConnectionString = _context.Database.GetDbConnection().ConnectionString;
                var builder = new SqlConnectionStringBuilder(currentConnectionString);
                var targetDbName = builder.InitialCatalog;

                if (string.IsNullOrWhiteSpace(targetDbName))
                {
                    _logger.LogError("Could not determine target database name from connection string.");
                    return new RestoreResponseDto
                    {
                        Success = false,
                        Message = "Could not determine target database name from connection string.",
                        RestoredAt = DateTimeOffset.UtcNow
                    };
                }

                var masterBuilder = new SqlConnectionStringBuilder(currentConnectionString)
                {
                    InitialCatalog = "master"
                };
                var masterConnectionString = masterBuilder.ConnectionString;

                using (var conn = new SqlConnection(masterConnectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"ALTER DATABASE [{targetDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                        _logger.LogInformation("Executing: {Sql}", cmd.CommandText);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            var escapedPath = tempFilePath.Replace("'", "''");
                            cmd.CommandText = $"RESTORE DATABASE [{targetDbName}] FROM DISK = '{escapedPath}' WITH REPLACE;";
                            _logger.LogInformation("Executing: {Sql}", cmd.CommandText);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    finally
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"ALTER DATABASE [{targetDbName}] SET MULTI_USER;";
                            _logger.LogInformation("Executing: {Sql}", cmd.CommandText);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                _logger.LogInformation("Database restore completed successfully for backup id {BackupId}", backupId);
                return new RestoreResponseDto
                {
                    Success = true,
                    Message = "Database restored successfully.",
                    RestoredAt = DateTimeOffset.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database restore for backup id {BackupId}", backupId);
                return new RestoreResponseDto
                {
                    Success = false,
                    Message = $"Error during restore: {ex.Message}",
                    RestoredAt = DateTimeOffset.UtcNow
                };
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInformation("Deleted temporary backup file {TempFilePath}", tempFilePath);
                    }
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete temporary backup file {TempFilePath}", tempFilePath);
                }
            }
        }
    }
}