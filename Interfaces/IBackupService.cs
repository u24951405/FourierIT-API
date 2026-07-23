using FourierIT_API.DTOs;
using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IBackupService
    {
        Task<BackupResponseDto> CreateDatabaseBackupAsync(CreateBackupRequestDto request);
        Task<IEnumerable<Backup>> GetAllBackupsAsync();
    }
}
