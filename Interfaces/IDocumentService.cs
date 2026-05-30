using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadDocumentAsync(string userId, string fileName, byte[] fileData);
        Task<byte[]> DownloadDocumentAsync(int documentId, string userId);
        Task<bool> ShareDocumentAsync(int documentId, string grantToUserId, AccessLevel accessLevel, string ownerUserId);
        Task<List<Document>> GetAccessibleDocumentAsync(string userId);
    }
}
