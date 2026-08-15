using System.IO;

namespace FourierIT_API.Interfaces
{
    public enum FileScanStatus
    {
        Clean,
        Infected,
        Error
    }

    public sealed class FileScanResult
    {
        public FileScanStatus Status { get; init; }
        public string Message { get; init; } = string.Empty;

        public bool IsClean => Status == FileScanStatus.Clean;

        public static FileScanResult Clean(string message = "File is clean.") => new() { Status = FileScanStatus.Clean, Message = message };
        public static FileScanResult Infected(string message) => new() { Status = FileScanStatus.Infected, Message = message };
        public static FileScanResult Error(string message) => new() { Status = FileScanStatus.Error, Message = message };
    }

    public interface IFileScanService
    {
        Task<FileScanResult> ScanFileAsync(Stream file);
    }
}
