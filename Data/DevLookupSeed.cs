using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Data
{
    /// <summary>Ensures minimum lookup rows exist so departments can reference a valid Branch (development convenience).</summary>
    public static class DevLookupSeed
    {
        private static readonly (string Id, string Name)[] DevelopmentRoles =
        {
            ("CFO", "ComplianceOfficer")
        };

        /// <summary>Financial-sector institution types for <c>InstitutionType</c> (inserted when missing).</summary>
        private static readonly string[] FinancialInstitutionTypeNames =
        {
            "Asset Management",
            "Commercial Bank",
            "Corporate Treasury / Holding",
            "Credit Provider (non-bank)",
            "Exchange / Trading Venue",
            "FinTech",
            "Insurance Underwriter",
            "Investment Bank",
            "Microfinance Institution",
            "Pension Fund Administrator",
            "Payment Service Provider",
            "Regulatory / Supervisory Body",
            "Retail Banking Group",
            "Stockbroker / Wealth Manager",
            "Other Financial Services",
        };

        private static readonly (string InstitutionName, string VerifiedDomain, int RegNumber, string InstitutionTypeName)[] SampleInstitutions =
        {
            ("Fourier IT", "fourier.local", 1, "Other Financial Services"),
            ("Standard Bank Group", "standardbank.co.za", 1002, "Commercial Bank"),
            ("Old Mutual Insurance", "oldmutual.com", 1003, "Insurance Underwriter"),
            ("Sanlam Investments", "sanlam.co.za", 1004, "Asset Management"),
            ("Investec Investment Bank", "investec.com", 1005, "Investment Bank"),
            ("JSE Limited", "jse.co.za", 1006, "Exchange / Trading Venue"),
            ("Yoco Payments", "yoco.co.za", 1007, "FinTech"),
            ("Capitec Credit Solutions", "capitec.co.za", 1008, "Credit Provider (non-bank)"),
            ("Alexander Forbes Retirement Services", "alexforbes.co.za", 1009, "Pension Fund Administrator"),
            ("PayGate Payment Services", "paygate.co.za", 1010, "Payment Service Provider"),
            ("RMB Stockbrokers", "rmb.co.za", 1011, "Stockbroker / Wealth Manager"),
            ("Blue Financial Services", "bluefin.co.za", 1012, "Microfinance Institution"),
            ("Nedbank Retail Banking", "nedbank.co.za", 1013, "Retail Banking Group"),
            ("Financial Sector Conduct Authority", "fsca.co.za", 1014, "Regulatory / Supervisory Body"),
            ("Absa Corporate Treasury", "absa.co.za", 1015, "Corporate Treasury / Holding"),
        };

        public static async Task EnsureBranchesExistAsync(AppDbContext db, CancellationToken ct = default)
        {
            await EnsureFinancialInstitutionTypesAsync(db, ct);
            await EnsureSampleInstitutionsAsync(db, ct);

            var typeId = await ResolveDefaultInstitutionTypeIdAsync(db, ct);

            var institutionId = await ResolveSeedInstitutionIdAsync(db, ct);

            if (!await db.Branches.AnyAsync(ct))
            {
                db.Branches.AddRange(
                    new Branch { BranchName = "Head Office — Johannesburg", City = "Johannesburg", InstitutionId = institutionId },
                    new Branch { BranchName = "London Branch", City = "London", InstitutionId = institutionId },
                    new Branch { BranchName = "Singapore Branch", City = "Singapore", InstitutionId = institutionId });

                await db.SaveChangesAsync(ct);
            }

            await EnsureMissingNamedBranchesAsync(db, institutionId, ct);
        }

        public static async Task EnsureDepartmentsAndRequirementsAsync(AppDbContext db, CancellationToken ct = default)
        {
            var institutionId = await ResolveSeedInstitutionIdAsync(db, ct);
            var branch = await db.Branches
                .Where(b => b.InstitutionId == institutionId)
                .OrderBy(b => b.BranchId)
                .FirstOrDefaultAsync(ct);

            if (branch == null)
                return;

            var departments = new[]
            {
                new Department { DepartmentName = "Fourier IT Innovation", BranchId = branch.BranchId, CreatedAt = DateTimeOffset.UtcNow },
                new Department { DepartmentName = "Fourier-E Consultation", BranchId = branch.BranchId, CreatedAt = DateTimeOffset.UtcNow },
                new Department { DepartmentName = "RQTech", BranchId = branch.BranchId, CreatedAt = DateTimeOffset.UtcNow },
                new Department { DepartmentName = "Fourier Recruitment", BranchId = branch.BranchId, CreatedAt = DateTimeOffset.UtcNow }
            };

            foreach (var department in departments)
            {
                var exists = await db.Departments.AnyAsync(d => d.DepartmentName == department.DepartmentName, ct);
                if (!exists) db.Departments.Add(department);
            }

            await db.SaveChangesAsync(ct);

            var documentTypes = await db.DocumentTypes.AsNoTracking().ToListAsync(ct);
            var allDocumentTypeIds = documentTypes.Select(dt => dt.DocumentTypeId).ToHashSet();

            var departmentLookup = await db.Departments.AsNoTracking().ToListAsync(ct);
            var departmentIds = departmentLookup.Select(d => d.DepartmentId).ToList();
            var existingPairs = await db.DepartmentDocumentTypes
                .AsNoTracking()
                .Where(ddt => departmentIds.Contains(ddt.DepartmentId))
                .Select(ddt => new { ddt.DepartmentId, ddt.DocumentTypeId })
                .ToListAsync(ct);

            var existingPairSet = existingPairs
                .Select(p => $"{p.DepartmentId}:{p.DocumentTypeId}")
                .ToHashSet();

            var requirements = new List<DepartmentDocumentType>();

            foreach (var department in departmentLookup)
            {
                var departmentName = department.DepartmentName;
                var requiredIds = departmentName switch
                {
                    "Fourier IT Innovation" => new[] { 1, 2, 7, 10, 11, 12 },
                    "Fourier-E Consultation" => new[] { 1, 4, 7, 10, 11, 12, 13 },
                    "RQTech" => new[] { 1, 2, 7, 8, 11, 13, 14, 18 },
                    "Fourier Recruitment" => new[] { 1, 2, 4, 7, 10 },
                    _ => Array.Empty<int>()
                };

                var departmentDocTypeIds = requiredIds
                    .Where(docTypeId => docTypeId >= 11 && docTypeId <= 19)
                    .Where(allDocumentTypeIds.Contains);

                foreach (var docTypeId in departmentDocTypeIds)
                {
                    var key = $"{department.DepartmentId}:{docTypeId}";
                    if (!existingPairSet.Contains(key))
                    {
                        requirements.Add(new DepartmentDocumentType
                        {
                            DepartmentId = department.DepartmentId,
                            DocumentTypeId = docTypeId,
                            IsMandatory = true,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                        existingPairSet.Add(key);
                    }
                }
            }

            if (requirements.Count > 0)
            {
                db.DepartmentDocumentTypes.AddRange(requirements);
                await db.SaveChangesAsync(ct);
            }
        }

        public static async Task EnsureRolesExistAsync(AppDbContext db, CancellationToken ct = default)
        {
            var existingRoleNames = await db.Roles
                .AsNoTracking()
                .Select(r => r.Name ?? string.Empty)
                .ToListAsync(ct);

            var existingRoleSet = existingRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rolesToAdd = new List<Role>();

            foreach (var (id, name) in DevelopmentRoles)
            {
                if (!existingRoleSet.Contains(name))
                {
                    rolesToAdd.Add(new Role
                    {
                        Id = id,
                        Name = name,
                        NormalizedName = name.ToUpperInvariant()
                    });
                    existingRoleSet.Add(name);
                }
            }

            if (rolesToAdd.Count > 0)
            {
                db.Roles.AddRange(rolesToAdd);
                await db.SaveChangesAsync(ct);
            }
        }

        private static async Task EnsureFinancialInstitutionTypesAsync(AppDbContext db, CancellationToken ct)
        {
            var existingNames = await db.InstitutionTypes
                .AsNoTracking()
                .Select(t => t.InstitutionTypeName)
                .ToListAsync(ct);

            var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var typesToAdd = new List<InstitutionType>();

            foreach (var name in FinancialInstitutionTypeNames)
            {
                if (!existingSet.Contains(name))
                {
                    typesToAdd.Add(new InstitutionType { InstitutionTypeName = name });
                    existingSet.Add(name);
                }
            }

            if (typesToAdd.Count > 0)
            {
                db.InstitutionTypes.AddRange(typesToAdd);
                await db.SaveChangesAsync(ct);
            }
        }

        private static async Task EnsureSampleInstitutionsAsync(AppDbContext db, CancellationToken ct)
        {
            var existingInstitutionNames = await db.Institutions
                .AsNoTracking()
                .Select(i => i.InstitutionName)
                .ToListAsync(ct);

            var existingInstitutionSet = existingInstitutionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var typeMap = await db.InstitutionTypes
                .AsNoTracking()
                .ToDictionaryAsync(t => t.InstitutionTypeName, t => t.InstitutionTypeId, ct);

            var institutionsToAdd = new List<Institution>();

            foreach (var (name, domain, regNumber, typeName) in SampleInstitutions)
            {
                if (existingInstitutionSet.Contains(name))
                    continue;

                if (!typeMap.TryGetValue(typeName, out var institutionTypeId))
                    continue;

                institutionsToAdd.Add(new Institution
                {
                    InstitutionName = name,
                    VerifiedDomain = domain,
                    RegNumber = regNumber,
                    TypeId = institutionTypeId
                });
                existingInstitutionSet.Add(name);
            }

            if (institutionsToAdd.Count > 0)
            {
                db.Institutions.AddRange(institutionsToAdd);
                await db.SaveChangesAsync(ct);
            }
        }

        private static async Task<int> ResolveSeedInstitutionIdAsync(AppDbContext db, CancellationToken ct)
        {
            const string preferred = "Fourier IT";
            var id = await db.Institutions
                .Where(i => i.InstitutionName == preferred)
                .Select(i => i.InstitutionId)
                .FirstOrDefaultAsync(ct);

            if (id != 0)
                return id;

            return await db.Institutions
                .OrderBy(i => i.InstitutionId)
                .Select(i => i.InstitutionId)
                .FirstAsync(ct);
        }

        /// <summary>Prefer a sensible default for seeded demo data; otherwise first type by id.</summary>
        private static async Task<int> ResolveDefaultInstitutionTypeIdAsync(AppDbContext db, CancellationToken ct)
        {
            const string preferred = "Other Financial Services";
            var id = await db.InstitutionTypes
                .Where(t => t.InstitutionTypeName == preferred)
                .Select(t => t.InstitutionTypeId)
                .FirstOrDefaultAsync(ct);

            if (id != 0)
                return id;

            return await db.InstitutionTypes
                .OrderBy(t => t.InstitutionTypeId)
                .Select(t => t.InstitutionTypeId)
                .FirstAsync(ct);
        }

        /// <summary>Adds commonly used branches when missing (e.g. Centurion on databases that already had other branches).</summary>
        private static async Task EnsureMissingNamedBranchesAsync(AppDbContext db, int institutionId, CancellationToken ct)
        {
            var defs = new (string BranchName, string City)[]
            {
                ("Head Office — Johannesburg", "Johannesburg"),
                ("London Branch", "London"),
                ("Singapore Branch", "Singapore"),
                ("Centurion", "Centurion"),
            };

            var existingBranchNames = await db.Branches
                .AsNoTracking()
                .Select(b => b.BranchName)
                .ToListAsync(ct);

            var existingSet = existingBranchNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = new List<Branch>();

            foreach (var (name, city) in defs)
            {
                if (!existingSet.Contains(name))
                {
                    toAdd.Add(new Branch { BranchName = name, City = city, InstitutionId = institutionId });
                    existingSet.Add(name);
                }
            }

            if (toAdd.Count > 0)
            {
                db.Branches.AddRange(toAdd);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
