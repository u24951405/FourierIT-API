using FourierIT_API.Data;
using FourierIT_API.DTOs.DocumentType;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Services;

public class DocumentValidityPolicyService
{
    private readonly AppDbContext _context;
    private readonly DocumentValidityCalculator _documentValidityCalculator;
    private readonly IComplianceService _complianceService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DocumentValidityPolicyService> _logger;

    public DocumentValidityPolicyService(
        AppDbContext context,
        DocumentValidityCalculator documentValidityCalculator,
        IComplianceService complianceService,
        IAuditLogService auditLogService,
        ILogger<DocumentValidityPolicyService> logger)
    {
        _context = context;
        _documentValidityCalculator = documentValidityCalculator;
        _complianceService = complianceService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<DocumentTypeValiditySummaryDto> PreviewAsync(int documentTypeId, DocumentTypeValidityUpdateRequest request)
    {
        var documentType = await _context.DocumentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.DocumentTypeId == documentTypeId)
            ?? throw new KeyNotFoundException($"Document type {documentTypeId} was not found.");

        var documents = await _context.Documents
            .AsNoTracking()
            .Include(d => d.CertificationDetails)
            .Where(d => d.DocumentTypeId == documentTypeId)
            .ToListAsync();

        return BuildSummary(documentType, documents, request);
    }

    public async Task<DocumentTypeValiditySummaryDto> ApplyAsync(int documentTypeId, DocumentTypeValidityUpdateRequest request, string actingUserId)
    {
        var documentType = await _context.DocumentTypes
            .FirstOrDefaultAsync(dt => dt.DocumentTypeId == documentTypeId)
            ?? throw new KeyNotFoundException($"Document type {documentTypeId} was not found.");

        var documents = await _context.Documents
            .Include(d => d.CertificationDetails)
            .Include(d => d.DocumentStatusHistories)
            .Where(d => d.DocumentTypeId == documentTypeId)
            .ToListAsync();

        var summary = BuildSummary(documentType, documents, request);
        var oldSettings = new DocumentValiditySettingsSnapshot
        {
            ValidityMonths = documentType.ValidityMonths,
            NeverExpires = documentType.NeverExpires,
            ValidityBasis = documentType.ValidityBasis,
            WarningDays = documentType.WarningDays
        };
        var now = DateTimeOffset.UtcNow;

        if (_context.Database.IsRelational())
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            await ApplyValidityChangesAsync(documentType, documents, request, now, actingUserId, oldSettings, summary);
            await transaction.CommitAsync();
            return summary;
        }

        await ApplyValidityChangesAsync(documentType, documents, request, now, actingUserId, oldSettings, summary);
        return summary;
    }

    private async Task ApplyValidityChangesAsync(
        DocumentType documentType,
        IReadOnlyCollection<Document> documents,
        DocumentTypeValidityUpdateRequest request,
        DateTimeOffset now,
        string actingUserId,
        DocumentValiditySettingsSnapshot oldSettings,
        DocumentTypeValiditySummaryDto summary)
    {
        documentType.ValidityMonths = request.ValidityMonths;
        documentType.NeverExpires = request.NeverExpires;
        documentType.ValidityBasis = request.ValidityBasis;
        documentType.WarningDays = request.WarningDays;

        foreach (var document in documents)
        {
            var previousExpired = string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                || document.ExpiryDate <= now;

            var proposedType = new DocumentType
            {
                ValidityMonths = request.ValidityMonths,
                NeverExpires = request.NeverExpires,
                ValidityBasis = request.ValidityBasis,
                WarningDays = request.WarningDays
            };

            var certificationDate = document.CertificationDetails
                .OrderByDescending(detail => detail.CertificationDate)
                .Select(detail => (DateTimeOffset?)detail.CertificationDate)
                .FirstOrDefault();

            var recalculatedExpiry = _documentValidityCalculator.Calculate(proposedType, document.UploadedDate, certificationDate).ExpiryDate;
            document.ExpiryDate = recalculatedExpiry;

            if (recalculatedExpiry <= now)
            {
                document.CurrentStatus = "Expired";
            }
            else if (previousExpired)
            {
                document.CurrentStatus = ResolveRestoredStatus(document);
            }
        }

        var affectedCount = documents.Count;
        var newlyExpiredCount = documents.Count(document =>
            string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
            && (document.ExpiryDate <= now));

        await _context.SaveChangesAsync();

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = actingUserId,
            ActionCode = "DOCUMENT_TYPE_VALIDITY_UPDATED",
            TimeStamp = DateTimeOffset.UtcNow,
            Description = $"Updated document validity for type '{documentType.TypeName}' from {{ validityMonths: {oldSettings.ValidityMonths}, neverExpires: {oldSettings.NeverExpires}, validityBasis: {oldSettings.ValidityBasis}, warningDays: {oldSettings.WarningDays} }} to {{ validityMonths: {documentType.ValidityMonths}, neverExpires: {documentType.NeverExpires}, validityBasis: {documentType.ValidityBasis}, warningDays: {documentType.WarningDays} }}. Affected documents: {affectedCount}. Newly expired: {newlyExpiredCount}.",
            TableAffected = "DocumentTypes",
            RecordID = documentType.DocumentTypeId
        });

        await _context.SaveChangesAsync();

        var complianceRecalculationFailed = false;
        var ownerIds = documents
            .Select(d => d.UserId)
            .Distinct()
            .ToList();

        foreach (var userId in ownerIds)
        {
            try
            {
                await _complianceService.CheckUserComplianceAsync(userId);
            }
            catch (Exception ex)
            {
                complianceRecalculationFailed = true;
                _logger.LogError(ex, "Failed to recalculate compliance after document validity update for user {UserId}", userId);
            }
        }

        summary.ComplianceRecalculationFailed = complianceRecalculationFailed;
    }

    private static string ResolveRestoredStatus(Document document)
    {
        var priorStatus = document.DocumentStatusHistories
            .OrderByDescending(history => history.DateArchived)
            .Select(history => history.StatusName)
            .FirstOrDefault(status => !string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase));

        return !string.IsNullOrWhiteSpace(priorStatus)
            ? priorStatus
            : "Pending";
    }

    private sealed class DocumentValiditySettingsSnapshot
    {
        public int ValidityMonths { get; set; }
        public bool NeverExpires { get; set; }
        public ValidityBasis ValidityBasis { get; set; }
        public int WarningDays { get; set; }
    }

    private DocumentTypeValiditySummaryDto BuildSummary(DocumentType documentType, IReadOnlyCollection<Document> documents, DocumentTypeValidityUpdateRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var summary = new DocumentTypeValiditySummaryDto
        {
            AffectedDocuments = documents.Count,
            BecomeExpired = 0,
            NoLongerExpired = 0,
            MissingSourceDate = 0
        };

        foreach (var document in documents)
        {
            var currentExpired = string.Equals(document.CurrentStatus, "Expired", StringComparison.OrdinalIgnoreCase)
                || document.ExpiryDate <= now;

            var proposedType = new DocumentType
            {
                ValidityMonths = request.ValidityMonths,
                NeverExpires = request.NeverExpires,
                ValidityBasis = request.ValidityBasis,
                WarningDays = request.WarningDays
            };

            var certificationDate = document.CertificationDetails
                .OrderByDescending(detail => detail.CertificationDate)
                .Select(detail => (DateTimeOffset?)detail.CertificationDate)
                .FirstOrDefault();

            var proposedExpiry = _documentValidityCalculator.Calculate(proposedType, document.UploadedDate, certificationDate).ExpiryDate;
            var missingSourceDate = request.ValidityBasis == ValidityBasis.CertificationDate && certificationDate is null;
            var wouldExpire = !request.NeverExpires && !missingSourceDate && proposedExpiry <= now;

            if (missingSourceDate)
            {
                summary.MissingSourceDate++;
            }

            if (!currentExpired && wouldExpire)
            {
                summary.BecomeExpired++;
            }

            if (currentExpired && !wouldExpire && !missingSourceDate)
            {
                summary.NoLongerExpired++;
            }
        }

        return summary;
    }
}
