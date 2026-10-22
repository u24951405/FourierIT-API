using System.Security.Claims;
using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

/// <summary>
/// An institution must be able to request a document type that the target document owner or
/// department hasn't uploaded yet — the request itself becomes the requirement for them to
/// upload it (surfaced via GetRequiredDocumentsStatus). These endpoints used to hard-reject
/// the request up front instead, which made it impossible to ever request a missing document.
/// </summary>
public class DocumentAccessRequestUploadRequirementTests
{
    [Fact]
    public async Task CreateInstitutionRequest_Individual_TargetHasNotUploadedRequestedDocumentType_StillCreatesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution" };
        var actor = CreateUser("actor-user", "Actor", "Person");
        var targetUser = CreateUser("target-user", "Target", "Owner");
        var documentType = new DocumentType { DocumentTypeId = 901, TypeName = "South African ID Book", Description = "ID document" };
        var sessionToken = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TokenString = "test-session-token-1",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        context.Institutions.Add(institution);
        context.InstitutionSessionTokens.Add(sessionToken);
        context.Users.AddRange(actor, targetUser);
        context.DocumentTypes.Add(documentType);
        await context.SaveChangesAsync();

        var controller = CreateController(context, actor);

        var result = await controller.CreateInstitutionRequest(
            sessionToken.TokenString,
            new InstitutionDocumentRequestDto
            {
                RequestType = "Individual",
                TargetUserId = targetUser.Id,
                PurposeNote = "Please provide your ID",
                RequestedDocuments = new List<RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = documentType.DocumentTypeId, IsMandatory = true }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await context.InstitutionEnquiryRequests.CountAsync());
        Assert.Equal(1, await context.InstitutionRequestedDocumentTypes.CountAsync());
    }

    [Fact]
    public async Task CreateInstitutionRequest_Department_NoMemberHasUploadedRequestedDocumentType_StillCreatesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 2, InstitutionName = "Test Institution 2" };
        var sessionToken = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            TokenString = "session-token-2",
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        var actor = CreateUser("actor-user-2", "Actor", "Person");
        var branch = new Branch { BranchId = 1, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 1, DepartmentName = "Operations", BranchId = branch.BranchId, Branch = branch };
        var documentType = new DocumentType { DocumentTypeId = 902, TypeName = "Utility Bill", Description = "Proof of address" };
        var departmentUser = CreateUser("dept-user", "Dept", "User");
        departmentUser.DepartmentId = department.DepartmentId;

        context.Institutions.Add(institution);
        context.InstitutionSessionTokens.Add(sessionToken);
        context.Users.AddRange(actor, departmentUser);
        context.Branches.Add(branch);
        context.Departments.Add(department);
        context.DocumentTypes.Add(documentType);
        context.DepartmentDocumentTypes.Add(new DepartmentDocumentType
        {
            DepartmentId = department.DepartmentId,
            Department = department,
            DocumentTypeId = documentType.DocumentTypeId,
            DocumentType = documentType,
            IsMandatory = true
        });
        context.InstitutionSessionTokens.Add(sessionToken);
        await context.SaveChangesAsync();

        var controller = CreateController(context, actor);

        var result = await controller.CreateInstitutionRequest(
            sessionToken.TokenString,
            new InstitutionDocumentRequestDto
            {
                RequestType = "Department",
                TargetDepartmentId = department.DepartmentId,
                PurposeNote = "Please provide proof of address",
                RequestedDocuments = new List<RequestedDocumentTypeDto>
                {
                    new() { DocumentTypeId = documentType.DocumentTypeId, IsMandatory = true }
                }
            });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await context.InstitutionEnquiryRequests.CountAsync());
    }

    [Fact]
    public async Task RouteRequestToOwner_OwnerHasNotUploadedRequestedDocumentType_StillRoutesRequest()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 3, InstitutionName = "Test Institution 3" };
        var branch = new Branch { BranchId = 2, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 2, DepartmentName = "Compliance", BranchId = branch.BranchId, Branch = branch };
        var departmentAdmin = CreateUser("dept-admin", "Admin", "Person");
        departmentAdmin.DepartmentId = department.DepartmentId;
        var owner = CreateUser("doc-owner", "Owner", "Person");
        owner.DepartmentId = department.DepartmentId;
        var documentType = new DocumentType { DocumentTypeId = 903, TypeName = "Passport", Description = "Travel document" };

        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = institution.InstitutionId,
            RequestType = "Department",
            TargetDepartmentId = department.DepartmentId,
            Status = "Department_Pending",
            PurposeNote = "Please provide a passport",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Institutions.Add(institution);
        context.Branches.Add(branch);
        context.Departments.Add(department);
        context.Users.AddRange(departmentAdmin, owner);
        context.DocumentTypes.Add(documentType);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            DocumentTypeId = documentType.DocumentTypeId,
            FICARuleId = 0,
            isMandatory = true
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, departmentAdmin);

        var result = await controller.RouteRequestToOwner(
            request.EnquiryRequestId,
            new RouteRequestToOwnerDto { TargetUserId = owner.Id });

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Pending", updated.Status);
        Assert.Equal(owner.Id, updated.TargetUserId);
    }

    [Fact]
    public async Task GetAssignableOwners_ListsOnlyTheDepartmentsMembers_WithWhatTheyHaveUploaded()
    {
        await using var context = CreateContext();
        var institution = new Institution { InstitutionId = 60, InstitutionName = "Owners Institution" };
        var branch = new Branch { BranchId = 60, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 60, DepartmentName = "Finance", BranchId = branch.BranchId, Branch = branch };
        var otherDepartment = new Department { DepartmentId = 61, DepartmentName = "Legal", BranchId = branch.BranchId, Branch = branch };
        var departmentAdmin = CreateUser("owners-admin", "Admin", "Person");
        departmentAdmin.DepartmentId = department.DepartmentId;
        var owner = CreateUser("owners-owner", "Owner", "Person");
        owner.DepartmentId = department.DepartmentId;
        var outsider = CreateUser("owners-outsider", "Out", "Sider");
        outsider.DepartmentId = otherDepartment.DepartmentId;
        var documentType = new DocumentType { DocumentTypeId = 960, TypeName = "Passport", Description = "Travel document" };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 60,
            InstitutionId = institution.InstitutionId,
            RequestType = "Department",
            TargetDepartmentId = department.DepartmentId,
            Status = "Department_Pending",
            PurposeNote = "Passport please",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Institutions.Add(institution);
        context.Branches.Add(branch);
        context.Departments.AddRange(department, otherDepartment);
        context.Users.AddRange(departmentAdmin, owner, outsider);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(new Document
        {
            DocumentId = 60,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "passport.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        });
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            DocumentTypeId = documentType.DocumentTypeId,
            FICARuleId = 0,
            isMandatory = true
        });
        await context.SaveChangesAsync();

        var result = await CreateController(context, departmentAdmin).GetAssignableOwners(request.EnquiryRequestId);

        var members = Assert.IsAssignableFrom<IEnumerable<object>>(Assert.IsType<OkObjectResult>(result).Value).ToList();
        Assert.Equal(new[] { owner.Id, departmentAdmin.Id }, members.Select(m => (string)Property(m, "UserId")!));
        Assert.Equal(1, Property(members[0], "UploadedTypes"));
        Assert.Equal(0, Property(members[1], "UploadedTypes"));

        var refused = await CreateController(context, outsider).GetAssignableOwners(request.EnquiryRequestId);
        Assert.IsType<ForbidResult>(refused);
    }

    [Fact]
    public async Task Approve_WhenNotAllRequestedDocumentTypesAreUploaded_ReturnsBadRequestWithMissingTypes()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-partial", "Owner", "Partial");
        var institutionType = new InstitutionType { InstitutionTypeId = 901, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution", VerifiedDomain = "test.co.za", RegNumber = 1001, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var idType = new DocumentType { DocumentTypeId = 904, TypeName = "South African ID Book", Description = "ID document" };
        var billType = new DocumentType { DocumentTypeId = 905, TypeName = "Utility Bill", Description = "Proof of address" };
        var uploadedDocument = new Document
        {
            DocumentId = 1,
            UserId = owner.Id,
            DocumentTypeId = idType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 1,
            InstitutionId = 1,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide ID and proof of address",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.AddRange(idType, billType);
        context.Documents.Add(uploadedDocument);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.AddRange(
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = idType.DocumentTypeId, FICARuleId = 0, isMandatory = true },
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = billType.DocumentTypeId, FICARuleId = 0, isMandatory = true });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var missingIds = badRequest.Value!.GetType().GetProperty("missingDocumentTypeIds")!.GetValue(badRequest.Value) as System.Collections.IEnumerable;
        Assert.Contains(billType.DocumentTypeId, missingIds!.Cast<int>());

        var unchanged = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Pending", unchanged.Status);
        Assert.Equal(0, await context.DocumentAccessApprovals.CountAsync());
    }

    [Fact]
    public async Task Approve_WhenAllRequestedDocumentTypesAreUploaded_Succeeds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-complete", "Owner", "Complete");
        var institutionType = new InstitutionType { InstitutionTypeId = 902, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 1, InstitutionName = "Test Institution", VerifiedDomain = "test2.co.za", RegNumber = 1002, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var idType = new DocumentType { DocumentTypeId = 906, TypeName = "South African ID Book", Description = "ID document" };
        var uploadedDocument = new Document
        {
            DocumentId = 2,
            UserId = owner.Id,
            DocumentTypeId = idType.DocumentTypeId,
            FileName = "id.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 2,
            InstitutionId = 1,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide ID",
            RequestDate = DateTimeOffset.UtcNow
        };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.Add(idType);
        context.Documents.Add(uploadedDocument);
        context.InstitutionEnquiryRequests.Add(request);
        var complianceStatus = new ComplianceStatus { UserId = owner.Id, User = owner };
        context.ComplianceStatuses.Add(complianceStatus);
        context.InstitutionRequestedDocumentTypes.Add(
            new InstitutionRequestedDocumentType { EnquiryRequestId = request.EnquiryRequestId, DocumentTypeId = idType.DocumentTypeId, FICARuleId = 0, isMandatory = true });
        await context.SaveChangesAsync();
        context.DocumentComplianceChecks.Add(new DocumentComplianceCheck
        {
            CheckId = 1,
            DocumentId = uploadedDocument.DocumentId,
            ComplianceStatusId = complianceStatus.ComplianceStatusId,
            CheckStatus = "Compliant",
            RequiresManualReview = true,
            IsManuallyApproved = true,
            ManualReviewDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.InstitutionEnquiryRequests.SingleAsync(r => r.EnquiryRequestId == request.EnquiryRequestId);
        Assert.Equal("Approved", updated.Status);
        Assert.Equal(1, await context.DocumentAccessApprovals.CountAsync());
    }

    [Fact]
    public async Task Approve_WhenRequestedDocumentHasNotBeenApprovedByCompliance_ReturnsConflict()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner-unreviewed", "Owner", "Unreviewed");
        var institutionType = new InstitutionType { InstitutionTypeId = 903, InstitutionTypeName = "Bank" };
        var institution = new Institution { InstitutionId = 3, InstitutionName = "Review Institution", VerifiedDomain = "review.test", RegNumber = 1003, TypeId = institutionType.InstitutionTypeId, InstitutionType = institutionType };
        var documentType = new DocumentType { DocumentTypeId = 907, TypeName = "Proof of Address", Description = "Address document" };
        var document = new Document
        {
            DocumentId = 3,
            UserId = owner.Id,
            DocumentTypeId = documentType.DocumentTypeId,
            FileName = "address.pdf",
            CurrentStatus = "Uploaded",
            ExpiryDate = DateTimeOffset.UtcNow.AddYears(1),
            FileSizeBytes = 10,
            EncryptionAlgorithm = "AES-256",
            IsEncrypted = true
        };
        var request = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 3,
            InstitutionId = institution.InstitutionId,
            RequestType = "Individual",
            TargetUserId = owner.Id,
            Status = "Pending",
            PurposeNote = "Please provide proof of address",
            RequestDate = DateTimeOffset.UtcNow
        };
        var status = new ComplianceStatus { UserId = owner.Id, User = owner };

        context.Users.Add(owner);
        context.InstitutionTypes.Add(institutionType);
        context.Institutions.Add(institution);
        context.DocumentTypes.Add(documentType);
        context.Documents.Add(document);
        context.InstitutionEnquiryRequests.Add(request);
        context.InstitutionRequestedDocumentTypes.Add(new InstitutionRequestedDocumentType
        {
            EnquiryRequestId = request.EnquiryRequestId,
            DocumentTypeId = documentType.DocumentTypeId,
            FICARuleId = 0,
            isMandatory = true
        });
        context.ComplianceStatuses.Add(status);
        await context.SaveChangesAsync();
        context.DocumentComplianceChecks.Add(new DocumentComplianceCheck
        {
            CheckId = 2,
            DocumentId = document.DocumentId,
            ComplianceStatusId = status.ComplianceStatusId,
            CheckStatus = "Pending Review",
            RequiresManualReview = true,
            IsManuallyApproved = false
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner);

        var result = await controller.Approve(request.EnquiryRequestId);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("Compliance Officer", conflict.Value!.GetType().GetProperty("error")!.GetValue(conflict.Value)!.ToString());
        Assert.Equal("Pending", (await context.InstitutionEnquiryRequests.SingleAsync()).Status);
        Assert.Empty(await context.DocumentAccessApprovals.ToListAsync());
    }

    [Fact]
    public void ReferenceNumber_IsBuiltFromTheRequestYearAndId()
    {
        var requested = new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal("REQ-2026-000123", InstitutionEnquiryRequest.BuildReferenceNumber(123, requested));
        Assert.Equal("REQ-2026-1234567", InstitutionEnquiryRequest.BuildReferenceNumber(1234567, requested));
    }

    [Fact]
    public async Task CreateInstitutionRequest_GeneratesAUniqueReferenceNumberForEachRequest()
    {
        await using var context = CreateContext();
        var (controller, token, documentTypeId, targetUserId) = await SeedIndividualRequestAsync(context);

        var first = Assert.IsType<OkObjectResult>(await controller.CreateInstitutionRequest(token, IndividualRequest(targetUserId, documentTypeId)));
        var second = Assert.IsType<OkObjectResult>(await controller.CreateInstitutionRequest(token, IndividualRequest(targetUserId, documentTypeId)));

        var saved = await context.InstitutionEnquiryRequests.OrderBy(r => r.EnquiryRequestId).ToListAsync();
        Assert.Equal(2, saved.Count);
        foreach (var request in saved)
            Assert.Equal(InstitutionEnquiryRequest.BuildReferenceNumber(request.EnquiryRequestId, request.RequestDate), request.ReferenceNumber);
        Assert.NotEqual(saved[0].ReferenceNumber, saved[1].ReferenceNumber);

        // The institution is shown the generated reference straight away.
        Assert.Equal(saved[0].ReferenceNumber, Property(first.Value, "referenceNumber"));
        Assert.Equal(saved[1].ReferenceNumber, Property(second.Value, "referenceNumber"));
    }

    [Fact]
    public async Task UpdateInstitutionRequest_KeepsTheGeneratedReferenceNumber()
    {
        await using var context = CreateContext();
        var (controller, token, documentTypeId, targetUserId) = await SeedIndividualRequestAsync(context);
        Assert.IsType<OkObjectResult>(await controller.CreateInstitutionRequest(token, IndividualRequest(targetUserId, documentTypeId)));
        var created = await context.InstitutionEnquiryRequests.SingleAsync();
        var generated = created.ReferenceNumber;

        var result = await controller.UpdateInstitutionRequest(created.EnquiryRequestId, token, new UpdateInstitutionDocumentRequestDto
        {
            PurposeNote = "Updated justification",
            RequestedDocuments = new List<RequestedDocumentTypeDto> { new() { DocumentTypeId = documentTypeId, IsMandatory = true } }
        });

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.InstitutionEnquiryRequests.AsNoTracking().SingleAsync();
        Assert.Equal("Updated justification", updated.PurposeNote);
        Assert.Equal(generated, updated.ReferenceNumber);
    }

    private static async Task<(DocumentAccessRequestsController Controller, string Token, int DocumentTypeId, string TargetUserId)>
        SeedIndividualRequestAsync(AppDbContext context)
    {
        var institution = new Institution { InstitutionId = 40, InstitutionName = "Reference Test Institution" };
        var actor = CreateUser("ref-actor", "Actor", "Person");
        var targetUser = CreateUser("ref-target", "Target", "Owner");
        var documentType = new DocumentType { DocumentTypeId = 940, TypeName = "Utility Bill", Description = "Proof of address" };
        var sessionToken = new InstitutionSessionToken
        {
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            TokenString = "reference-session-token",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.Institutions.Add(institution);
        context.InstitutionSessionTokens.Add(sessionToken);
        context.Users.AddRange(actor, targetUser);
        context.DocumentTypes.Add(documentType);
        await context.SaveChangesAsync();
        return (CreateController(context, actor), sessionToken.TokenString, documentType.DocumentTypeId, targetUser.Id);
    }

    private static InstitutionDocumentRequestDto IndividualRequest(string targetUserId, int documentTypeId) => new()
    {
        RequestType = "Individual",
        TargetUserId = targetUserId,
        PurposeNote = "Please provide proof of address",
        RequestedDocuments = new List<RequestedDocumentTypeDto> { new() { DocumentTypeId = documentTypeId, IsMandatory = true } }
    };

    private static object? Property(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);

    // The Super Admin has no "Super Admin" role; the token carries a superadmin claim instead (see TokenService).
    private static void SignInAsSuperAdmin(DocumentAccessRequestsController controller) =>
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("superadmin", "true") }, "Test"));

    private static async Task<(InstitutionEnquiryRequest Individual, InstitutionEnquiryRequest Department, User SuperAdmin)> SeedPendingRequestsAsync(AppDbContext context)
    {
        var institution = new Institution { InstitutionId = 50, InstitutionName = "Super Admin Test Institution" };
        var branch = new Branch { BranchId = 50, InstitutionId = institution.InstitutionId, Institution = institution, BranchName = "HQ" };
        var department = new Department { DepartmentId = 50, DepartmentName = "Finance", BranchId = branch.BranchId, Branch = branch };
        var owner = CreateUser("sa-owner", "Owner", "Person");
        var superAdmin = CreateUser("sa-super", "Super", "Admin");
        var documentType = new DocumentType { DocumentTypeId = 950, TypeName = "Bank Statement", Description = "Statement" };
        var individual = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 51, InstitutionId = institution.InstitutionId, RequestType = "Individual", TargetUserId = owner.Id,
            Status = "Pending", PurposeNote = "For the owner", RequestDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var forDepartment = new InstitutionEnquiryRequest
        {
            EnquiryRequestId = 52, InstitutionId = institution.InstitutionId, RequestType = "Department", TargetDepartmentId = department.DepartmentId,
            Status = "Department_Pending", PurposeNote = "For the department", RequestDate = DateTimeOffset.UtcNow
        };
        context.Institutions.Add(institution);
        context.Branches.Add(branch);
        context.Departments.Add(department);
        context.Users.AddRange(owner, superAdmin);
        context.DocumentTypes.Add(documentType);
        context.InstitutionEnquiryRequests.AddRange(individual, forDepartment);
        context.InstitutionRequestedDocumentTypes.AddRange(
            new InstitutionRequestedDocumentType { EnquiryRequestId = individual.EnquiryRequestId, DocumentTypeId = documentType.DocumentTypeId, isMandatory = true },
            new InstitutionRequestedDocumentType { EnquiryRequestId = forDepartment.EnquiryRequestId, DocumentTypeId = documentType.DocumentTypeId, isMandatory = true });
        await context.SaveChangesAsync();
        return (individual, forDepartment, superAdmin);
    }

    [Fact]
    public async Task SuperAdmin_SeesEveryPendingRequestInBothLists()
    {
        await using var context = CreateContext();
        var (individual, forDepartment, superAdmin) = await SeedPendingRequestsAsync(context);
        var controller = CreateController(context, superAdmin);
        SignInAsSuperAdmin(controller);

        var ownerList = Assert.IsType<OkObjectResult>(await controller.GetPendingRequests());
        var departmentList = Assert.IsType<OkObjectResult>(await controller.GetPendingDepartmentRequests());

        // Before, the department list was refused (403) and the owner list only showed requests addressed to the Super Admin.
        Assert.Equal(new[] { individual.EnquiryRequestId }, Ids(ownerList.Value));
        Assert.Equal(new[] { forDepartment.EnquiryRequestId }, Ids(departmentList.Value));
    }

    [Fact]
    public async Task SuperAdmin_CanOpenARequestsDocuments()
    {
        await using var context = CreateContext();
        var (individual, _, superAdmin) = await SeedPendingRequestsAsync(context);
        var controller = CreateController(context, superAdmin);
        SignInAsSuperAdmin(controller);

        Assert.IsType<OkObjectResult>(await controller.GetRequestDocuments(individual.EnquiryRequestId));
    }

    [Fact]
    public async Task OtherUsers_StillCannotListDepartmentRequestsOrOthersRequests()
    {
        await using var context = CreateContext();
        await SeedPendingRequestsAsync(context);
        var someoneElse = CreateUser("sa-other", "Some", "One");
        context.Users.Add(someoneElse);
        await context.SaveChangesAsync();
        var controller = CreateController(context, someoneElse);

        Assert.IsType<ForbidResult>(await controller.GetPendingDepartmentRequests());
        Assert.Empty(Ids(Assert.IsType<OkObjectResult>(await controller.GetPendingRequests()).Value));
    }

    private static int[] Ids(object? list) =>
        ((System.Collections.IEnumerable)list!).Cast<object>().Select(item => (int)Property(item, "EnquiryRequestId")!).ToArray();

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentAccessRequestUploadRequirementTests_{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static User CreateUser(string id, string firstName, string lastName)
    {
        var user = new User { Id = id, UserName = $"{id}@test.com", NormalizedUserName = $"{id.ToUpperInvariant()}@TEST.COM" };
        user.Profile = new Profile
        {
            UserId = id,
            User = user,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = "0123456789",
            JobTitle = "Tester"
        };
        return user;
    }

    private static DocumentAccessRequestsController CreateController(AppDbContext context, User actor)
    {
        var userStore = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(
            userStore.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(actor);
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .Returns<string>(async id => await context.Users.FirstOrDefaultAsync(u => u.Id == id));

        var documentService = new Mock<IDocumentService>();
        var complianceService = new Mock<IComplianceService>();
        complianceService
            .Setup(service => service.CheckUserComplianceAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((string userId, bool _) => new ComplianceStatus { UserId = userId });
        var departmentValidation = new FourierIT_API.Services.DepartmentRequestValidationService();
        var auditLog = new Mock<IAuditLogService>();

        var controller = new DocumentAccessRequestsController(
            context,
            userManager.Object,
            documentService.Object,
            departmentValidation,
            auditLog.Object,
            complianceService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        return controller;
    }
}
