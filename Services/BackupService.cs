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
            _context = context;
            _env = env;
            _configuration = configuration;
            _logger = logger;
            _blobServiceClient = blobServiceClient;
        }

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

            // Determine database name from the DbContext's connection
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

                // Execute SQL backup command
                string sql = $"BACKUP DATABASE [{dbName}] TO DISK = '{tempFilePath.Replace("'", "''")}' WITH FORMAT, INIT;";
                await _context.Database.ExecuteSqlRawAsync(sql);

                _logger.LogInformation("Database backup finished, uploading to Azure Blob Storage container '{Container}'", containerName);

                // Ensure container exists
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                // Upload the file
                var blobClient = containerClient.GetBlobClient(fileName);

                using (var fileStream = File.OpenRead(tempFilePath))
                {
                    await blobClient.UploadAsync(fileStream, overwrite: true);
                }

                string blobUrl = blobClient.Uri.ToString();

                // -------------------------------------------------------------
                // Check if the provided UserId exists in AspNetUsers.
                // If not found (e.g. testing with "1"), fall back to null.
                // -------------------------------------------------------------
                string? validUserId = null;
                if (!string.IsNullOrWhiteSpace(request.UserId))
                {
                    bool userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
                    if (userExists)
                    {
                        validUserId = request.UserId;
                    }
                }
                // Save metadata to database
                var backup = new Backup
                {
                    UserId = request.UserId,
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

        public async Task<IEnumerable<Backup>> GetAllBackupsAsync()
        {
            return await _context.Backups.OrderByDescending(b => b.DateBackedUp).ToListAsync();
        }
    }
}
