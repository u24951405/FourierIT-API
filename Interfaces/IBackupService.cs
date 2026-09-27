using FourierIT_API.DTOs;
using FourierIT_API.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FourierIT_API.Interfaces
{
    public interface IBackupService
    {
        Task<BackupResponseDto> CreateDatabaseBackupAsync(CreateBackupRequestDto request);
        Task<IEnumerable<Backup>> GetAllBackupsAsync();
        /// <summary>
        /// Restore the database from a historical backup identified by backupId.
        /// </summary>
        /// <param name="backupId">The id of the backup record in the database.</param>
        /// <returns>A <see cref="RestoreResponseDto"/> describing success/failure and timestamp.</returns>
        /// <param name="restoredByUserId">Who asked for the restore, for the audit trail.</param>
        Task<RestoreResponseDto> RestoreDatabaseAsync(int backupId, string? restoredByUserId = null);
    }
}
