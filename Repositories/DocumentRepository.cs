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
            var previousCommandTimeout = _context.Database.GetCommandTimeout();
            _context.Database.SetCommandTimeout(120);
            try
            {
                await _context.SaveChangesAsync();
            }
            finally
            {
                _context.Database.SetCommandTimeout(previousCommandTimeout);
            }

            return document;
        }

        public async Task<Document?> DeleteDocumentAsync(int id)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var doc = await _context.Documents.FirstOrDefaultAsync(d => d.DocumentId == id);
                if (doc == null) return null;

                var documentBlobs = await _context.DocumentBlobs
                    .Include(db => db.BlobHistories)
                    .Where(db => db.DocumentId == id)
                    .ToListAsync();

                var certificationDetails = await _context.CertificationDetails
                    .Where(cd => cd.DocumentId == id)
                    .ToListAsync();

                var accessLists = await _context.AccessLists
                    .Where(al => al.DocumentId == id)
                    .ToListAsync();

                var statusHistories = await _context.DocumentStatusHistories
                    .Where(dsh => dsh.DocumentId == id)
                    .ToListAsync();

                var complianceChecks = await _context.DocumentComplianceChecks
                    .Where(dc => dc.DocumentId == id)
                    .ToListAsync();

                var accessLogs = await _context.DocumentAccessLogs
                    .Where(dal => dal.DocumentId == id)
                    .ToListAsync();

                var complianceAlerts = await _context.ComplianceAlerts
                    .Where(ca => ca.DocumentId == id)
                    .ToListAsync();

                var complianceAuditLogs = await _context.ComplianceAuditLogs
                    .Where(cal => cal.DocumentId == id)
                    .ToListAsync();

                var accessApprovals = await _context.DocumentAccessApprovals
                    .Where(daa => daa.DocumentId == id)
                    .ToListAsync();

                var ruleComplianceChecks = await _context.ComplianceChecks
                    .Where(cc => cc.DocumentId == id)
                    .ToListAsync();

                foreach (var history in statusHistories)
                    history.DocumentId = null;

                foreach (var check in complianceChecks)
                    check.DocumentId = null;

                foreach (var check in ruleComplianceChecks)
                    check.DocumentId = null;

                _context.BlobHistories.RemoveRange(documentBlobs.SelectMany(db => db.BlobHistories));
                _context.DocumentBlobs.RemoveRange(documentBlobs);
                _context.CertificationDetails.RemoveRange(certificationDetails);
                _context.AccessLists.RemoveRange(accessLists);
                _context.DocumentAccessLogs.RemoveRange(accessLogs);
                _context.ComplianceAlerts.RemoveRange(complianceAlerts);
                _context.ComplianceAuditLogs.RemoveRange(complianceAuditLogs);
                _context.DocumentAccessApprovals.RemoveRange(accessApprovals);

                await _context.SaveChangesAsync();

                _context.Documents.Remove(doc);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return doc;
            });
        }

        public async Task<Document?> GetDocumentByIdAsync(int id)
        {
            return await _context.Documents
            .Include(d => d.User)
                .ThenInclude(u => u.Profile)
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
            // Documents loaded through this context are already tracked, so SaveChanges writes only what changed.
            // Update() would mark the whole loaded graph (owner, profile, file blob...) as modified and rewrite all of it,
            // including the file bytes, on every save, even when only LastAccessedDate changed.
            if (_context.Entry(document).State == EntityState.Detached)
                _context.Documents.Update(document);

            await _context.SaveChangesAsync();
        }
    }
}
