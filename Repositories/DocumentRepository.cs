using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models; // Ensure this using is present for FourierIT_API.Models.Document
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FourierIT_API.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _context;

        public DocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Document> AddDocumentAsync(Document document)
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<Document?> DeleteDocumentAsync(int id)
        {
            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.DocumentId == id);
            if (doc == null) return null;
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
            return doc;
        }

        public async Task<Document> GetDocumentByIdAsync(int id)
        {
            return await _context.Documents
            .Include(d => d.SharedWith)
            .Include(d => d.DocumentBlob)
            .Include(d => d.DocumentType)
            .Include(d => d.CertificationDetails)
            .FirstOrDefaultAsync(d => d.DocumentId == id);
        }

        public async Task<List<Document>> GetUserDocumentAsync(string UserId)
        {
            return await _context.Documents
                .Where(d => d.UserId == UserId)
                .Include(d => d.SharedWith)
                .Include(d => d.DocumentBlob)
                .Include(d => d.DocumentType)
                .ToListAsync();
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();
        }
    }
}
