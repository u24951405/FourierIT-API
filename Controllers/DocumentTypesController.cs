using System.Security.Claims;
using FourierIT_API.Data;
using FourierIT_API.DTOs.DocumentType;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Controllers;

[ApiController]
[Route("api/document-types")]
[Authorize(Policy = "SuperAdminOnly")]
public class DocumentTypesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly DocumentValidityPolicyService _documentValidityPolicyService;

    public DocumentTypesController(AppDbContext context, DocumentValidityPolicyService documentValidityPolicyService)
    {
        _context = context;
        _documentValidityPolicyService = documentValidityPolicyService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentTypeSummaryDto>>> GetAll()
    {
        var documentTypes = await _context.DocumentTypes
            .AsNoTracking()
            .Include(dt => dt.Documents)
            .OrderBy(dt => dt.TypeName)
            .Select(dt => new DocumentTypeSummaryDto
            {
                Id = dt.DocumentTypeId,
                Name = dt.TypeName,
                Description = dt.Description,
                ValidityMonths = dt.ValidityMonths,
                NeverExpires = dt.NeverExpires,
                ValidityBasis = dt.ValidityBasis,
                WarningDays = dt.WarningDays,
                DocumentCount = dt.Documents.Count
            })
            .ToListAsync();

        return Ok(documentTypes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentTypeSummaryDto>> GetById(int id)
    {
        var documentType = await _context.DocumentTypes
            .AsNoTracking()
            .Include(dt => dt.Documents)
            .Where(dt => dt.DocumentTypeId == id)
            .Select(dt => new DocumentTypeSummaryDto
            {
                Id = dt.DocumentTypeId,
                Name = dt.TypeName,
                Description = dt.Description,
                ValidityMonths = dt.ValidityMonths,
                NeverExpires = dt.NeverExpires,
                ValidityBasis = dt.ValidityBasis,
                WarningDays = dt.WarningDays,
                DocumentCount = dt.Documents.Count
            })
            .FirstOrDefaultAsync();

        if (documentType == null)
            return NotFound(new { message = $"Document type {id} was not found." });

        return Ok(documentType);
    }

    [HttpPost("{id:int}/validity/preview")]
    public async Task<ActionResult<DocumentTypeValiditySummaryDto>> PreviewValidity(int id, [FromBody] DocumentTypeValidityUpdateRequest request)
    {
        if (!IsSuperAdminUser())
            return Forbid();

        if (!TryValidateRequest(request, out var validationMessage))
            return BadRequest(new { message = validationMessage });

        try
        {
            var result = await _documentValidityPolicyService.PreviewAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/validity")]
    public async Task<ActionResult<DocumentTypeValiditySummaryDto>> UpdateValidity(int id, [FromBody] DocumentTypeValidityUpdateRequest request)
    {
        if (!IsSuperAdminUser())
            return Forbid();

        if (!TryValidateRequest(request, out var validationMessage))
            return BadRequest(new { message = validationMessage });

        try
        {
            var actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.Identity?.Name
                ?? "system";

            var result = await _documentValidityPolicyService.ApplyAsync(id, request, actingUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private bool IsSuperAdminUser()
    {
        return User.HasClaim("superadmin", "true")
            || User.IsInRole("Super Admin")
            || User.IsInRole("SUPER ADMIN");
    }

    private static bool TryValidateRequest(DocumentTypeValidityUpdateRequest request, out string message)
    {
        if (request == null)
        {
            message = "The validity payload is required.";
            return false;
        }

        if (request.ValidityMonths < 1 || request.ValidityMonths > 120)
        {
            message = "ValidityMonths must be between 1 and 120.";
            return false;
        }

        if (request.WarningDays < 0 || request.WarningDays > 365)
        {
            message = "WarningDays must be between 0 and 365.";
            return false;
        }

        if (!request.NeverExpires && request.WarningDays >= request.ValidityMonths * 30)
        {
            message = $"WarningDays must be less than validityMonths * 30 ({request.ValidityMonths * 30}).";
            return false;
        }

        if (!Enum.IsDefined(typeof(ValidityBasis), request.ValidityBasis))
        {
            message = "ValidityBasis must be a valid enum value.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
