using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FourierIT_API.Data
{
    public static class DevelopmentDataSeeder
    {
        private const string MarkerInstitutionName = "Demo Meridian Finance";
        private const string SeedPassword = "DemoSeed!2026";

        public static async Task SeedAsync(
            AppDbContext db,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            IDocumentService documentService,
            IComplianceService complianceService,
            CancellationToken cancellationToken = default)
        {
            if (await db.Institutions.AnyAsync(i => i.InstitutionName == MarkerInstitutionName, cancellationToken))
                return;

            var institutionType = await db.InstitutionTypes
                .OrderBy(t => t.InstitutionTypeId)
                .FirstAsync(cancellationToken);
            var documentTypes = await db.DocumentTypes
                .OrderBy(t => t.DocumentTypeId)
                .Take(8)
                .ToListAsync(cancellationToken);
            if (documentTypes.Count < 4)
                throw new InvalidOperationException("Development seeding requires at least four document types.");

            var institutions = Enumerable.Range(1, 3)
                .Select(index => new Institution
                {
                    InstitutionName = $"Demo {new[] { "Meridian", "Northstar", "Cedar" }[index - 1]} Finance",
                    VerifiedDomain = $"demo-{index}.invalid",
                    RegNumber = 900000 + index,
                    TypeId = institutionType.InstitutionTypeId
                })
                .ToList();
            db.Institutions.AddRange(institutions);
            await db.SaveChangesAsync(cancellationToken);

            var departments = new List<Department>();
            for (var institutionIndex = 0; institutionIndex < institutions.Count; institutionIndex++)
            {
                var branch = new Branch
                {
                    BranchName = $"Demo Branch {institutionIndex + 1}",
                    City = $"Demo City {institutionIndex + 1}",
                    InstitutionId = institutions[institutionIndex].InstitutionId
                };
                db.Branches.Add(branch);
                await db.SaveChangesAsync(cancellationToken);

                for (var departmentIndex = 0; departmentIndex < 2; departmentIndex++)
                {
                    var department = new Department
                    {
                        DepartmentName = $"Demo {institutionIndex + 1}-{departmentIndex + 1} Operations",
                        BranchId = branch.BranchId,
                        CreatedAt = DateTimeOffset.UtcNow.AddMonths(-(institutionIndex + departmentIndex + 1))
                    };
                    departments.Add(department);
                    db.Departments.Add(department);
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            var requiredTypes = documentTypes.Take(4).ToList();
            var requirements = departments.SelectMany(department => requiredTypes.Select(documentType => new DepartmentDocumentType
            {
                DepartmentId = department.DepartmentId,
                DocumentTypeId = documentType.DocumentTypeId,
                IsMandatory = true,
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6)
            })).ToList();
            db.DepartmentDocumentTypes.AddRange(requirements);
            await db.SaveChangesAsync(cancellationToken);

            var documentOwnerRole = await EnsureRoleAsync(roleManager, "Document Owner");
            var departmentAdminRole = await EnsureRoleAsync(roleManager, "Department Admin");
            var users = new List<User>();
            var admins = new List<User>();

            for (var index = 0; index < departments.Count; index++)
            {
                var department = departments[index];
                var admin = await CreateUserAsync(userManager, departmentAdminRole, new User
                {
                    UserName = $"demo.deptadmin.{index + 1}",
                    Email = $"deptadmin{index + 1}@demo.invalid",
                    PhoneNumber = $"08000000{index + 1:00}",
                    AccountStatus = "Active",
                    EmailConfirmed = true,
                    DepartmentId = department.DepartmentId,
                    EntityTypeId = 3,
                    EntityIdentificationNumber = $"DEMO-ADMIN-{index + 1:00}"
                }, $"DemoAdmin{index + 1}", "Coordinator", SeedPassword, cancellationToken);
                admins.Add(admin);

                for (var ownerIndex = 0; ownerIndex < 2; ownerIndex++)
                {
                    var owner = await CreateUserAsync(userManager, documentOwnerRole, new User
                    {
                        UserName = $"demo.owner.{index + 1}.{ownerIndex + 1}",
                        Email = $"owner{index + 1}{ownerIndex + 1}@demo.invalid",
                        PhoneNumber = $"08100000{(index * 2) + ownerIndex + 1:00}",
                        AccountStatus = "Active",
                        EmailConfirmed = true,
                        // A department's only user is its Department Admin, so owners are not placed in departments.
                        EntityTypeId = 3,
                        EntityIdentificationNumber = $"DEMO-OWNER-{index + 1:00}{ownerIndex + 1:00}"
                    }, $"DemoOwner{index + 1}{ownerIndex + 1}", "Analyst", SeedPassword, cancellationToken);
                    users.Add(owner);
                }
            }

            var allUsers = admins.Concat(users).ToList();
            var profileByUser = await db.Profiles
                .Where(profile => allUsers.Select(user => user.Id).Contains(profile.UserId))
                .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

            var documentIds = new List<Document>();
            var fileData = Encoding.ASCII.GetBytes("%PDF-1.4\n% Demo development document\n");
            var documentTypeIndex = 0;
            foreach (var owner in users)
            {
                for (var documentIndex = 0; documentIndex < 4; documentIndex++)
                {
                    var document = await documentService.UploadDocumentAsync(
                        owner.Id,
                        $"demo-{owner.UserName}-{documentIndex + 1}.pdf",
                        fileData,
                        requiredTypes[documentIndex].DocumentTypeId);
                    var createdAt = DateTime.UtcNow.AddMonths(-6).AddDays((documentIds.Count * 3) % 170);
                    document.UploadedDate = createdAt;
                    document.LastModifiedDate = createdAt.AddDays(1);
                    document.ExpiryDate = createdAt.AddMonths(documentIndex == 3 ? -1 : 18);
                    document.CurrentStatus = documentIndex switch
                    {
                        0 => "Approved",
                        1 => "Pending",
                        2 => "Rejected",
                        _ => "Expired"
                    };
                    documentIds.Add(document);
                    documentTypeIndex++;
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            foreach (var owner in users)
                await complianceService.CheckUserComplianceAsync(owner.Id);

            foreach (var owner in users.Take(6))
                await complianceService.CheckUserComplianceAsync(owner.Id);

            var statusHistories = documentIds.Select((document, index) => new DocumentStatusHistory
            {
                DocumentId = document.DocumentId,
                StatusName = document.CurrentStatus,
                DateArchived = document.UploadedDate.AddDays(1)
            }).ToList();
            db.DocumentStatusHistories.AddRange(statusHistories);

            var ownerByIndex = users.ToDictionary(user => user.Id);
            var accessLogs = documentIds.Select((document, index) => new DocumentAccessLog
            {
                DocumentId = document.DocumentId,
                AccessedByUserId = admins[index % admins.Count].Id,
                ActionType = index % 2 == 0 ? "Preview" : "Download",
                AccessDateTime = document.UploadedDate.AddDays(2),
                IPAddress = "192.0.2.10",
                UserAgent = "DemoSeed/1.0"
            }).ToList();
            db.DocumentAccessLogs.AddRange(accessLogs);

            var auditLogs = Enumerable.Range(0, 30).Select(index => new AuditLog
            {
                UserId = allUsers[index % allUsers.Count].Id,
                ActionCode = index % 3 == 0 ? "DOCUMENT_UPLOAD" : index % 3 == 1 ? "DOCUMENT_PREVIEW" : "COMPLIANCE_CHECK",
                TimeStamp = DateTimeOffset.UtcNow.AddMonths(-6).AddDays(index * 5),
                Description = "Development seed historical event.",
                TableAffected = index % 2 == 0 ? "Documents" : "ComplianceStatuses",
                RecordID = documentIds[index % documentIds.Count].DocumentId,
                PreviousBlockHash = index == 0 ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"previous-{index}"))),
                BlockHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"event-{index}")))
            }).ToList();
            db.AuditLogs.AddRange(auditLogs);

            var requests = new List<InstitutionEnquiryRequest>();
            var requestedTypes = new List<InstitutionRequestedDocumentType>();
            for (var index = 0; index < 16; index++)
            {
                var institution = institutions[index % institutions.Count];
                var department = departments[index % departments.Count];
                var request = new InstitutionEnquiryRequest
                {
                    InstitutionId = institution.InstitutionId,
                    TargetDepartmentId = department.DepartmentId,
                    TargetUserId = users[index % users.Count].Id,
                    RequestType = index % 2 == 0 ? "Department" : "Individual",
                    Status = (index % 4) switch
                    {
                        0 => "Approved",
                        1 => "Pending",
                        2 => "Denied",
                        _ => "Department_Pending"
                    },
                    PurposeNote = "Development seed enquiry for report validation.",
                    SubmissionDeadline = DateTimeOffset.UtcNow.AddDays(14 - index),
                    ReferenceNumber = $"DEMO-ENQ-{index + 1:000}",
                    RequestDate = DateTimeOffset.UtcNow.AddMonths(-5).AddDays(index * 9),
                    RespondedAt = index % 4 == 1 ? null : DateTime.UtcNow.AddMonths(-4).AddDays(index * 7),
                    ApprovedByUserId = admins[index % admins.Count].Id
                };
                requests.Add(request);
                db.InstitutionEnquiryRequests.Add(request);
            }
            await db.SaveChangesAsync(cancellationToken);

            var ficaRule = await db.FICARules
                .OrderBy(rule => rule.RuleId)
                .FirstOrDefaultAsync(cancellationToken);
            if (ficaRule == null)
            {
                ficaRule = new FICARule
                {
                    Description = "Default institution access request rule",
                    ValidityMonths = 48
                };
                db.FICARules.Add(ficaRule);
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var request in requests)
            {
                foreach (var documentType in requiredTypes.Take(2 + (request.EnquiryRequestId % 3)))
                {
                    requestedTypes.Add(new InstitutionRequestedDocumentType
                    {
                        EnquiryRequestId = request.EnquiryRequestId,
                        DocumentTypeId = documentType.DocumentTypeId,
                        FICARuleId = ficaRule.RuleId,
                        isMandatory = true
                    });
                }
            }
            db.InstitutionRequestedDocumentTypes.AddRange(requestedTypes);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var request in requests.Where(request => request.Status == "Approved"))
            {
                var document = documentIds[request.EnquiryRequestId % documentIds.Count];
                db.AccessTokens.Add(new AccessToken
                {
                    EnquiryRequestId = request.EnquiryRequestId,
                    TokenString = $"demo-token-{request.EnquiryRequestId}-{Guid.NewGuid():N}",
                    ExpiryTimeStamp = request.RequestDate.AddDays(request.EnquiryRequestId % 2 == 0 ? -1 : 30),
                    IsRevoked = false,
                    UserId = users[request.EnquiryRequestId % users.Count].Id
                });
                db.DocumentAccessApprovals.Add(new DocumentAccessApproval
                {
                    EnquiryRequestId = request.EnquiryRequestId,
                    DocumentId = document.DocumentId,
                    ApprovedByUserId = request.ApprovedByUserId!,
                    ApprovedAt = request.RespondedAt ?? DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    IsRevoked = request.EnquiryRequestId % 3 == 0,
                    RevokedAt = request.EnquiryRequestId % 3 == 0 ? DateTime.UtcNow.AddDays(-2) : null
                });
            }
            await db.SaveChangesAsync(cancellationToken);

            Console.WriteLine($"DevelopmentDataSeeder: institutions={institutions.Count}, branches={institutions.Count}, departments={departments.Count}, users={allUsers.Count}, profiles={profileByUser.Count}, documents={documentIds.Count}, requests={requests.Count}, requestedDocumentTypes={requestedTypes.Count}, accessApprovals={await db.DocumentAccessApprovals.CountAsync(cancellationToken)}, accessTokens={await db.AccessTokens.CountAsync(cancellationToken)}, statusHistory={statusHistories.Count}, accessLogs={accessLogs.Count}, auditLogs={auditLogs.Count}");
        }

        private static async Task<Role> EnsureRoleAsync(RoleManager<Role> roleManager, string roleName)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role != null) return role;

            role = new Role { Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create seed role {roleName}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            return role;
        }

        private static async Task<User> CreateUserAsync(
            UserManager<User> userManager,
            Role role,
            User user,
            string firstName,
            string lastName,
            string password,
            CancellationToken cancellationToken)
        {
            user.Profile = new Profile
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                JobTitle = lastName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create seed user {user.UserName}: {string.Join(", ", result.Errors.Select(error => error.Description))}");

            var roleResult = await userManager.AddToRoleAsync(user, role.Name!);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Could not assign seed role to {user.UserName}: {string.Join(", ", roleResult.Errors.Select(error => error.Description))}");

            return user;
        }
    }
}
