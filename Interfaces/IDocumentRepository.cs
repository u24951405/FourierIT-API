using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document> AddDocumentAsync(Document document);
        Task<Document> GetDocumentByIdAsync(int id);
        Task<List<Document>> GetUserDocumentAsync(string  UserId);
        Task<Document?> DeleteDocumentAsync(int id);
        Task UpdateDocumentAsync(Document document);
    }
}
