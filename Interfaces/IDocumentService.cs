using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadDocumentAsync(string userId, string fileName, byte[] fileData, int documentTypeId);
        Task<byte[]> DownloadDocumentAsync(int documentId, string userId);
    }
}
