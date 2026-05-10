using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;

namespace FourierIT_API.Data
{
    /// <summary>Ensures minimum lookup rows exist so departments can reference a valid Branch (development convenience).</summary>
    public static class DevLookupSeed
    {
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
            "Regulatory / Supervisory Body",
            "Retail Banking Group",
            "Stockbroker / Wealth Manager",
            "Other Financial Services",
        };

        public static async Task EnsureBranchesExistAsync(AppDbContext db, CancellationToken ct = default)
        {
            await EnsureFinancialInstitutionTypesAsync(db, ct);

            var typeId = await ResolveDefaultInstitutionTypeIdAsync(db, ct);

            if (!await db.Institutions.AnyAsync(ct))
            {
                db.Institutions.Add(new Institution
                {
                    InstitutionName = "Fourier IT",
                    VerifiedDomain = "fourier.local",
                    RegNumber = 1,
                    TypeId = typeId
                });
                await db.SaveChangesAsync(ct);
            }

            var institutionId = await db.Institutions.Select(i => i.InstitutionId).FirstAsync(ct);

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

        private static async Task EnsureFinancialInstitutionTypesAsync(AppDbContext db, CancellationToken ct)
        {
            foreach (var name in FinancialInstitutionTypeNames)
            {
                var exists = await db.InstitutionTypes.AnyAsync(t => t.InstitutionTypeName == name, ct);
                if (!exists)
                    db.InstitutionTypes.Add(new InstitutionType { InstitutionTypeName = name });
            }

            await db.SaveChangesAsync(ct);
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

            foreach (var (name, city) in defs)
            {
                if (await db.Branches.AnyAsync(b => b.BranchName == name, ct))
                    continue;

                db.Branches.Add(new Branch { BranchName = name, City = city, InstitutionId = institutionId });
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
