using FourierIT_API.Models;
using FourierIT_API.DTOs.Compliance;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FourierIT_API.Data
{
    public class AppDbContext : IdentityDbContext<User, Role, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        //dbSet<> returns data from tables/models, returns data in the form that you want. Your basically manipulating the whole table,
        //and it is going to going to create your database
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<Department> Departments { get; set; } = null!; // tells us that we have a table called Departments in our database and it is represented by the Department model and we grabbin[...]
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<InstitutionType> InstitutionTypes { get; set; } = null!;
        public DbSet<Institution> Institutions { get; set; }
        public DbSet<InstitutionInvitation> InstitutionInvitations { get; set; }
        public DbSet<InstitutionSessionToken> InstitutionSessionTokens { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        
        // Secure vault DbSets
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentAccess> DocumentAccesses { get; set; }
        public DbSet<DocumentAccessLog> DocumentAccessLogs { get; set; }

        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<DepartmentDocumentType> DepartmentDocumentTypes { get; set; }
        public DbSet<DocumentBlob> DocumentBlobs { get; set; }
        public DbSet<ProfileImageBlob> ProfileImageBlobs { get; set; }
        public DbSet<BlobHistory> BlobHistories { get; set; }
        public DbSet<FICARule> FICARules { get; set; }
        public DbSet<DocumentFicaRule> DocumentFicaRules { get; set; }
        public DbSet<CertificationDetails> CertificationDetails { get; set; }
        public DbSet<DocumentStatusHistory> DocumentStatusHistories { get; set; }
        public DbSet<AccessToken> AccessTokens { get; set; }
        public DbSet<AccessList> AccessLists { get; set; }
        public DbSet<InstitutionEnquiryRequest> InstitutionEnquiryRequests { get; set; }
        public DbSet<InstitutionRequestedDocumentType> InstitutionRequestedDocumentTypes { get; set; }
        public DbSet<DocumentAccessApproval> DocumentAccessApprovals { get; set; }
        public DbSet<EnquirySession> EnquirySessions { get; set; }
        public DbSet<EnquiryFlag> EnquiryFlags { get; set; }
        public DbSet<EnquiryComment> EnquiryComments { get; set; }
        public DbSet<EntityType> EntityTypes { get; set; }
        public DbSet<RequiredDocument> RequiredDocuments { get; set; }
        public DbSet<ClientEnlistment> ClientEnlistments { get; set; }
        public DbSet<PendingRegistration> PendingRegistrations { get; set; }

        // FICA Compliance System DbSets
        public DbSet<ComplianceStatus> ComplianceStatuses { get; set; }
        public DbSet<InstitutionComplianceSummaryDto> InstitutionComplianceSummaries { get; set; }
        public DbSet<DocumentComplianceCheck> DocumentComplianceChecks { get; set; }
        public DbSet<ComplianceHistory> ComplianceHistories { get; set; }
        public DbSet<ComplianceAlert> ComplianceAlerts { get; set; }
        public DbSet<ComplianceAuditLog> ComplianceAuditLogs { get; set; }
        public DbSet<ComplianceRule> ComplianceRules { get; set; }
        public DbSet<ComplianceRequirement> ComplianceRequirements { get; set; }
        public DbSet<ComplianceCheck> ComplianceChecks { get; set; }
        public DbSet<ComplianceResult> ComplianceResults { get; set; }
        public DbSet<AdHocReport> AdHocReports { get; set; }

        // System (Backup, Restore, Auditlog) Db Sets 
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Backup> Backups { get; set; }
        // Location DbSets
        public DbSet<Address> Addresses { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Suburb> Suburbs { get; set; }
        public DbSet<Province> Provinces { get; set; }

        // System DbSets
        // (Backups DbSet already declared above in System (Backup, Restore, Auditlog) section)
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<SecurityQuestion> SecurityQuestions { get; set; }
        public DbSet<FICARuleHistory> FICARuleHistories { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        // Risk Rating DbSets
        public DbSet<RiskVariable> RiskVariables { get; set; }
        public DbSet<RiskRatingVariable> RiskRatingVariables { get; set; }
        public DbSet<ClientRiskRating> ClientRiskRatings { get; set; }

        // User-Related DbSets
        public DbSet<UserSecurityQuestion> UserSecurityQuestions { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<InstitutionMembers> InstitutionMembers { get; set; }
        public new DbSet<UserRole> UserRoles { get; set; }
        public DbSet<PEPList> PEPLists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InstitutionComplianceSummaryDto>().HasNoKey().ToView(null);

            // Ensure Email is unique (in addition to UserName)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("[Email] IS NOT NULL");  // NULL emails are allowed

            List<Role> Roles = new List<Role>
            {
                new Role
                {
                Id = "AD",
                Name = "Admin",
                NormalizedName = "ADMIN"
                },
                new Role
                {
                Id = "DA",
                Name = "Department Admin",
                NormalizedName = "DEPARTMENT ADMIN"
                },
                new Role
                {
                Id = "DO",
                Name = "Document Owner",
                NormalizedName = "DOCUMENT OWNER"
                },
                new Role
                {
                Id = "CO",
                Name = "Compliance Officer",
                NormalizedName = "COMPLIANCE OFFICER"
                },
                new Role
                {
                Id = "SH",
                Name = "Stakeholder",
                NormalizedName = "STAKEHOLDER"
                }
            };
            modelBuilder.Entity<Role>().HasData(Roles);

            modelBuilder.Entity<Permission>().HasData(
                new Permission { PermissionId = 1, PermissionKey = "Documents.View" },
                new Permission { PermissionId = 2, PermissionKey = "Documents.Upload" },
                new Permission { PermissionId = 3, PermissionKey = "Documents.Manage" },
                new Permission { PermissionId = 4, PermissionKey = "Compliance.View" },
                new Permission { PermissionId = 5, PermissionKey = "Compliance.Manage" },
                new Permission { PermissionId = 6, PermissionKey = "Users.Manage" },
                new Permission { PermissionId = 7, PermissionKey = "Roles.Manage" },
                new Permission { PermissionId = 8, PermissionKey = "Reports.View" },
                new Permission { PermissionId = 9, PermissionKey = "Audit.View" },
                new Permission { PermissionId = 10, PermissionKey = "Backup.Manage" });

            modelBuilder.Entity<RolePermission>().HasData(
                new { RoleId = "AD", PermissionId = 1 }, new { RoleId = "AD", PermissionId = 2 },
                new { RoleId = "AD", PermissionId = 3 }, new { RoleId = "AD", PermissionId = 4 },
                new { RoleId = "AD", PermissionId = 5 }, new { RoleId = "AD", PermissionId = 6 },
                new { RoleId = "AD", PermissionId = 7 }, new { RoleId = "AD", PermissionId = 8 },
                new { RoleId = "AD", PermissionId = 9 }, new { RoleId = "AD", PermissionId = 10 },
                new { RoleId = "DA", PermissionId = 1 }, new { RoleId = "DA", PermissionId = 2 },
                new { RoleId = "DA", PermissionId = 3 }, new { RoleId = "DA", PermissionId = 4 },
                new { RoleId = "DA", PermissionId = 5 }, new { RoleId = "DA", PermissionId = 6 },
                new { RoleId = "DA", PermissionId = 7 }, new { RoleId = "DA", PermissionId = 8 },
                new { RoleId = "DO", PermissionId = 1 }, new { RoleId = "DO", PermissionId = 2 },
                new { RoleId = "CO", PermissionId = 1 }, new { RoleId = "CO", PermissionId = 4 },
                new { RoleId = "CO", PermissionId = 5 }, new { RoleId = "CO", PermissionId = 8 },
                new { RoleId = "SH", PermissionId = 1 }, new { RoleId = "SH", PermissionId = 4 });

            modelBuilder.Entity<SystemSetting>().HasData(
                new SystemSetting
                {
                    Key = "InstitutionOtpExpiryMinutes",
                    Value = "10",
                    Description = "Minutes before an institution portal OTP expires."
                },
                new SystemSetting
                {
                    Key = "InstitutionSessionTimeoutMinutes",
                    Value = "480",
                    Description = "Minutes before an institution portal session expires."
                });

            modelBuilder.Entity<Department>()
                .HasOne(d => d.Parent)
                .WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstitutionInvitation>()
                .HasIndex(ii => ii.TokenString)
                .HasDatabaseName("IX_InstitutionInvitations_TokenString");

            modelBuilder.Entity<InstitutionSessionToken>(entity =>
            {
                entity.Property(st => st.TokenString).HasMaxLength(255);
                entity.HasIndex(st => st.TokenString).HasDatabaseName("IX_InstitutionSessionTokens_TokenString");
            });

            // Seed DocumentTypes - FICA Compliant South African Requirements
            List<DocumentType> documentTypes = new List<DocumentType>
            {
                 // Individual - SA Citizen
                 new DocumentType { DocumentTypeId = 1, TypeName = "South African ID Book", Description = "Green bar-coded identity document (certified copy)" },
                 new DocumentType { DocumentTypeId = 2, TypeName = "Smart ID Card", Description = "South African Smart ID card (certified copy)" },
                 new DocumentType { DocumentTypeId = 3, TypeName = "South African Passport", Description = "Valid South African passport (certified copy)" },
    
                 // Individual - Foreign National
                 new DocumentType { DocumentTypeId = 4, TypeName = "Foreign Passport", Description = "Valid foreign passport (certified copy)" },
                 new DocumentType { DocumentTypeId = 5, TypeName = "Work Permit", Description = "Valid South African work permit or visa" },
                 new DocumentType { DocumentTypeId = 6, TypeName = "Asylum/Refugee Permit", Description = "Valid asylum seeker or refugee permit" },
    
                 // Proof of Address - All Individuals
                 new DocumentType { DocumentTypeId = 7, TypeName = "Utility Bill", Description = "Water/electricity bill (not older than 3 months)" },
                 new DocumentType { DocumentTypeId = 8, TypeName = "Telkom/Internet Account", Description = "Telecommunications or ISP account (not older than 3 months)" },
                 new DocumentType { DocumentTypeId = 9, TypeName = "Lease Agreement", Description = "Current residential lease agreement" },
                 new DocumentType { DocumentTypeId = 10, TypeName = "Bank Statement", Description = "Official bank statement showing address (not older than 3 months)" },
    
                 // Business/Corporate Documents
                new DocumentType { DocumentTypeId = 11, TypeName = "Certificate of Incorporation", Description = "COR14.3 or company registration certificate" },
                new DocumentType { DocumentTypeId = 12, TypeName = "Memorandum of Incorporation", Description = "MOI (Memorandum of Incorporation)" },
                new DocumentType { DocumentTypeId = 13, TypeName = "Company Resolution", Description = "Board resolution authorizing account/instruction" },
                new DocumentType { DocumentTypeId = 14, TypeName = "Shareholder Register", Description = "Current register of shareholders/members" },
    
                // Trust Documents
                new DocumentType { DocumentTypeId = 15, TypeName = "Trust Deed", Description = "Registered trust deed and letters of authority" },
                new DocumentType { DocumentTypeId = 16, TypeName = "Trust Resolution", Description = "Resolution from trustees authorizing the transaction" },
    
                // Partnership
                new DocumentType { DocumentTypeId = 17, TypeName = "Partnership Agreement", Description = "Registered partnership agreement" },
    
                // Authorized Person Documents (for entities)
                new DocumentType { DocumentTypeId = 18, TypeName = "Director/Trustee ID", Description = "ID of authorized representative (certified copy)" },
                new DocumentType { DocumentTypeId = 19, TypeName = "Proof of Address - Representative", Description = "Proof of residential address for authorized person" },

                // Department Compliance - KYC/FICA Onboarding Requirements
                new DocumentType { DocumentTypeId = 20, TypeName = "SARS Tax Clearance Certificate", Description = "Valid SARS Tax Clearance Certificate (not older than 12 months)" },
                new DocumentType { DocumentTypeId = 21, TypeName = "Bank Confirmation Letter", Description = "Bank confirmation of account and authorized signatories" },
                new DocumentType { DocumentTypeId = 22, TypeName = "Beneficial Ownership Declaration", Description = "Declaration of beneficial owners with shareholding details" }
            };
            modelBuilder.Entity<DocumentType>().HasData(documentTypes);

            // Seed EntityTypes
            List<EntityType> entityTypes = new List<EntityType>
            {
                new EntityType { EntityTypeId = 1, Name = "South African Individual" },
                new EntityType { EntityTypeId = 2, Name = "Foreign National Individual" },
                new EntityType { EntityTypeId = 3, Name = "Company (Pty) Ltd" },
                new EntityType { EntityTypeId = 4, Name = "Trust" },
                new EntityType { EntityTypeId = 5, Name = "Partnership" },
                new EntityType { EntityTypeId = 6, Name = "Legal Entity - Other" }
            };
            modelBuilder.Entity<EntityType>().HasData(entityTypes);

            // Seed RequiredDocuments - FICA compliant requirements per entity type
            List<RequiredDocument> requiredDocuments = new List<RequiredDocument>
            {
                // SA Individual - must have ID + Address proof
                new RequiredDocument { RequiredDocumentId = 1, EntityTypeId = 1, DocumentTypeId = 1, IsMandatory = true, Description = "One form of SA ID required" },
                new RequiredDocument { RequiredDocumentId = 2, EntityTypeId = 1, DocumentTypeId = 2, IsMandatory = false, Description = "Alternative to ID book" },
                new RequiredDocument { RequiredDocumentId = 3, EntityTypeId = 1, DocumentTypeId = 3, IsMandatory = false, Description = "Alternative to ID book" },
                new RequiredDocument { RequiredDocumentId = 4, EntityTypeId = 1, DocumentTypeId = 7, IsMandatory = true, Description = "One address proof required (not older than 3 months)" },
                new RequiredDocument { RequiredDocumentId = 5, EntityTypeId = 1, DocumentTypeId = 8, IsMandatory = false, Description = "Alternative address proof" },
                new RequiredDocument { RequiredDocumentId = 6, EntityTypeId = 1, DocumentTypeId = 9, IsMandatory = false, Description = "Alternative address proof" },
    
                // Foreign National - Passport + Permit + Address
                new RequiredDocument { RequiredDocumentId = 7, EntityTypeId = 2, DocumentTypeId = 4, IsMandatory = true, Description = "Valid foreign passport required" },
                new RequiredDocument { RequiredDocumentId = 8, EntityTypeId = 2, DocumentTypeId = 5, IsMandatory = true, Description = "Valid SA work permit/visa required" },
                new RequiredDocument { RequiredDocumentId = 9, EntityTypeId = 2, DocumentTypeId = 6, IsMandatory = false, Description = "Alternative to work permit" },
                new RequiredDocument { RequiredDocumentId = 10, EntityTypeId = 2, DocumentTypeId = 10, IsMandatory = true, Description = "Proof of address required" },
    
                // Company - Registration + MOI + Resolution + Representative
                new RequiredDocument { RequiredDocumentId = 11, EntityTypeId = 3, DocumentTypeId = 11, IsMandatory = true, Description = "Certificate of incorporation required" },
                new RequiredDocument { RequiredDocumentId = 12, EntityTypeId = 3, DocumentTypeId = 12, IsMandatory = true, Description = "MOI required" },
                new RequiredDocument { RequiredDocumentId = 13, EntityTypeId = 3, DocumentTypeId = 13, IsMandatory = true, Description = "Board resolution authorizing required" },
                new RequiredDocument { RequiredDocumentId = 14, EntityTypeId = 3, DocumentTypeId = 14, IsMandatory = true, Description = "Current shareholder register required" },
                new RequiredDocument { RequiredDocumentId = 15, EntityTypeId = 3, DocumentTypeId = 18, IsMandatory = true, Description = "ID of authorized director required" },
                new RequiredDocument { RequiredDocumentId = 16, EntityTypeId = 3, DocumentTypeId = 19, IsMandatory = true, Description = "Address proof for director required" },
    
                // Trust - Trust Deed + Resolution + Representative
                new RequiredDocument { RequiredDocumentId = 17, EntityTypeId = 4, DocumentTypeId = 15, IsMandatory = true, Description = "Trust deed and letters of authority required" },
                new RequiredDocument { RequiredDocumentId = 18, EntityTypeId = 4, DocumentTypeId = 16, IsMandatory = true, Description = "Trustee resolution required" },
                new RequiredDocument { RequiredDocumentId = 19, EntityTypeId = 4, DocumentTypeId = 18, IsMandatory = true, Description = "ID of authorized trustee required" },
                new RequiredDocument { RequiredDocumentId = 20, EntityTypeId = 4, DocumentTypeId = 19, IsMandatory = true, Description = "Address proof for trustee required" },
    
                // Partnership - Agreement + Resolution + Representative
                new RequiredDocument { RequiredDocumentId = 21, EntityTypeId = 5, DocumentTypeId = 17, IsMandatory = true, Description = "Partnership agreement required" },
                new RequiredDocument { RequiredDocumentId = 22, EntityTypeId = 5, DocumentTypeId = 13, IsMandatory = true, Description = "Partnership resolution required" },
                new RequiredDocument { RequiredDocumentId = 23, EntityTypeId = 5, DocumentTypeId = 18, IsMandatory = true, Description = "ID of authorized partner required" },
                new RequiredDocument { RequiredDocumentId = 24, EntityTypeId = 5, DocumentTypeId = 19, IsMandatory = true, Description = "Address proof for partner required" }
            };
            modelBuilder.Entity<RequiredDocument>().HasData(requiredDocuments);

            // Departments and related DepartmentDocumentTypes are seeded at runtime by DevLookupSeed
            // to avoid FK conflicts during migrations when Branch/Institution ids are not guaranteed.

            // Configure UserRole many-to-many relationship

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure RolePermission many-to-many relationship
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User-Profile one-to-one relationship with a shared primary key
            // inside OnModelCreating after base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Profile>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<Profile>()
                .HasOne(p => p.ProfileImageBlob)
                .WithOne(b => b.Profile)
                .HasForeignKey<ProfileImageBlob>(b => b.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Address-Profile one-to-one relationship
            modelBuilder.Entity<Address>()
                .HasOne(a => a.Profile)
                .WithOne(p => p.Address)
                .HasForeignKey<Profile>(p => p.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Suburb one-to-many relationship with Address
            modelBuilder.Entity<Suburb>()
                .HasMany( s=> s.Addresses)
                .WithOne( a => a.Suburb)
                .HasForeignKey(a => a.SuburbId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Province one-to-many relationship with City
            modelBuilder.Entity<Province>()
                .HasMany(p => p.Cities)
                .WithOne(c => c.Province)
                .HasForeignKey(c => c.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure City one-to-many relationship with suburb
            modelBuilder.Entity<City>()
                .HasMany(c => c.Suburbs)
                .WithOne(s => s.City)
                .HasForeignKey( s => s.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User one-to-many relationship with document
            modelBuilder.Entity<User>()
                .HasMany(u => u.Documents)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Institution)
                .WithMany()
                .HasForeignKey(a => a.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DocumentType one-to-many relationship with document
            modelBuilder.Entity<DocumentType>()
                .HasMany(dt => dt.Documents)
                .WithOne(d => d.DocumentType)
                .HasForeignKey(d => d.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DocumentFicaRule many-to-many relationship
            modelBuilder.Entity<DocumentFicaRule>()
                .HasKey(dfr => new { dfr.DocumentTypeId, dfr.FICARuleId });

            modelBuilder.Entity<DocumentFicaRule>()
                .HasOne(dfr => dfr.DocumentType)
                .WithMany(dt => dt.DocumentFicaRules)
                .HasForeignKey(dfr => dfr.DocumentTypeId);

            modelBuilder.Entity<DocumentFicaRule>()
                .HasOne(dfr => dfr.FICARule)
                .WithMany(fr => fr.DocumentFicaRules)
                .HasForeignKey(dfr => dfr.FICARuleId);

            // Configure FICARule one-to-many relationship with FICARuleHistory
            modelBuilder.Entity<FICARule>()
                .HasMany(fr => fr.FICARuleHistories)
                .WithOne(frh => frh.FICARule)
                .HasForeignKey(frh => frh.RuleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure UserSecurityQuestion many-to-many relationship
            modelBuilder.Entity<UserSecurityQuestion>()
                .HasKey(usq => new { usq.UserId, usq.SecurityQuestionId });

            modelBuilder.Entity<UserSecurityQuestion>()
                .HasOne(usq => usq.User)
                .WithMany(u => u.UserSecurityQuestions)
                .HasForeignKey(usq => usq.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserSecurityQuestion>()
                .HasOne(usq => usq.SecurityQuestion)
                .WithMany(sq => sq.UserSecurityQuestions)
                .HasForeignKey(usq => usq.SecurityQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure UserNotification many-to-many relationship
            modelBuilder.Entity<UserNotification>()
                .HasKey(un => new { un.NotificationId, un.UserId });

            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.Notification)
                .WithMany(n => n.UserNotifications)
                .HasForeignKey(un => un.NotificationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.User)
                .WithMany(u => u.UserNotifications)
                .HasForeignKey(un => un.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure document one-to-one relationship with DocumentBlob
            modelBuilder.Entity<Document>()
                .ToTable("Documents", table => table.HasTrigger("TR_Documents_SetLastModifiedDate"))
                .HasOne(d => d.DocumentBlob)
                .WithOne(db => db.Document)
                .HasForeignKey<DocumentBlob>(db => db.DocumentId);

            // Configure DocumentBlob one-to-many relationship with BlobHistory
            modelBuilder.Entity<DocumentBlob>()
                .HasMany(db => db.BlobHistories)
                .WithOne(bh => bh.DocumentBlob)
                .HasForeignKey(bh => bh.DocumentBlobId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Document one-to-many relationship with CertificationDetails
            modelBuilder.Entity<Document>()
                .HasMany(d => d.CertificationDetails)
                .WithOne(cd => cd.Document)
                .HasForeignKey(cd => cd.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Document one-to-many relationship with DocumentStatusHistory
            modelBuilder.Entity<Document>()
                .HasMany(d => d.DocumentStatusHistories)
                .WithOne(dsh => dsh.Document)
                .HasForeignKey(dsh => dsh.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure InstitutionType one-to-many with Institution
            modelBuilder.Entity<InstitutionType>()
                .ToTable("InstitutionType")
                .HasMany(it => it.Institutions)
                .WithOne(i => i.InstitutionType)
                .HasForeignKey(i => i.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Many-to-many relationship for InstitutionMembers
            modelBuilder.Entity<InstitutionMembers>()
                .HasKey(im => new { im.MembersId, im.UserId, im.InstitutionId });

            modelBuilder.Entity<InstitutionMembers>()
                .HasOne(im => im.Institution)
                .WithMany(i => i.InstitutionMembers)
                .HasForeignKey(im => im.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstitutionMembers>()
                .HasOne(im => im.User)
                .WithMany(u => u.InstitutionMembers)
                .HasForeignKey(im => im.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Institution One-to-many relationship with Branch
            modelBuilder.Entity<Institution>()
                .ToTable("Institutions")
                .HasMany(i => i.Branches)
                .WithOne(b => b.Institution)
                .HasForeignKey(b => b.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Branch one-to-many relationship with Department
            modelBuilder.Entity<Branch>()
                .ToTable("Branch")
                .HasMany(b => b.Departments)
                .WithOne(d => d.Branch)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DepartmentDocumentType relationships
            modelBuilder.Entity<DepartmentDocumentType>()
                .HasOne(ddt => ddt.Department)
                .WithMany(d => d.DepartmentDocumentTypes)
                .HasForeignKey(ddt => ddt.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DepartmentDocumentType>()
                .HasOne(ddt => ddt.DocumentType)
                .WithMany()
                .HasForeignKey(ddt => ddt.DocumentTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ClientEnlistment relationships
            modelBuilder.Entity<ClientEnlistment>()
                .HasKey(ce => new { ce.ClientEnlistmentId });

            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.User)
                .WithMany(u => u.ClientEnlistments)
                .HasForeignKey(ce => ce.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.Institution)
                .WithMany(i => i.ClientEnlistments)
                .HasForeignKey(ce => ce.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ClientEnlistment one-to-one relationship with ClientRiskRating
            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.ClientRiskRating)
                .WithOne(crr => crr.ClientEnlistment)
                .HasForeignKey<ClientRiskRating>(crr => crr.ClientEnlistmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure RiskRatingVariable relationships
            modelBuilder.Entity<RiskRatingVariable>()
                .HasKey(rrv => new { rrv.RatingId, rrv.RiskVariableId });

            modelBuilder.Entity<RiskRatingVariable>()
                .HasOne(rrv => rrv.ClientRiskRating)
                .WithMany(crr => crr.RiskRatingVariables)
                .HasForeignKey(rrv => rrv.RatingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RiskRatingVariable>()
                .HasOne(rrv => rrv.RiskVariable)
                .WithMany(rv => rv.RiskRatingVariables)
                .HasForeignKey(rrv => rrv.RiskVariableId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ClientRiskRating one-to-many relationship with RiskHistory
            modelBuilder.Entity<ClientRiskRating>()
                .HasMany(crr => crr.RiskHistories)
                .WithOne(rh => rh.ClientRiskRating)
                .HasForeignKey(rh => rh.ClientRiskRatingId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Institution one-to-many relationship with InstitutionEnquiryRequest
            modelBuilder.Entity<Institution>()
                .HasMany(i => i.institutionEnquiryRequests)
                .WithOne(ier => ier.Institution)
                .HasForeignKey(ier => ier.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InstitutionEnquiryRequest>()
                .HasOne(ier => ier.TargetUser)
                .WithMany()
                .HasForeignKey(ier => ier.TargetUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure InstitutionRequestedDocumentType relationships
            modelBuilder.Entity<InstitutionRequestedDocumentType>()
                .HasKey(irdt => new { irdt.EnquiryRequestId, irdt.DocumentTypeId });

            modelBuilder.Entity<InstitutionRequestedDocumentType>()
                .HasOne(irdt => irdt.InstitutionEnquiryRequest)
                .WithMany(ier => ier.RequestedDocumentTypes)
                .HasForeignKey(ier => ier.EnquiryRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InstitutionRequestedDocumentType>()
                .HasOne(irdt => irdt.DocumentType)
                .WithMany()
                .HasForeignKey(irdt => irdt.DocumentTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InstitutionRequestedDocumentType>()
                .HasOne(irdt => irdt.FICARule)
                .WithMany()
                .HasForeignKey(irdt => irdt.FICARuleId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure DocumentAccessApproval relationships
            modelBuilder.Entity<DocumentAccessApproval>()
                .HasOne(daa => daa.InstitutionEnquiryRequest)
                .WithMany()
                .HasForeignKey(daa => daa.EnquiryRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocumentAccessApproval>()
                .HasOne(daa => daa.Document)
                .WithMany()
                .HasForeignKey(daa => daa.DocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DocumentAccessApproval>()
                .HasOne(daa => daa.ApprovedByUser)
                .WithMany()
                .HasForeignKey(daa => daa.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure InstitutionEnquiryRequest one-to-one relationship with AccessToken
            modelBuilder.Entity<InstitutionEnquiryRequest>()
                .HasOne(ier => ier.AccessToken)
                .WithOne(at => at.institutionEnquiryRequest)
                .HasForeignKey<AccessToken>(at => at.EnquiryRequestId)
                .OnDelete(DeleteBehavior.Restrict);


            // Configure AccessToken one-to-one relationship with EnquirySession
            modelBuilder.Entity<AccessToken>()
                .HasOne(at => at.EnquirySession)
                .WithOne(es => es.AccessToken)
                .HasForeignKey<EnquirySession>(es => es.TokenId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure AccessToken one-to-one relationship with User **
            modelBuilder.Entity<AccessToken>()
                .HasOne(at => at.User)
                .WithOne(u => u.AccessToken)
                .HasForeignKey<AccessToken>(at => at.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User one-to-one relationship with EnquiryComment
            modelBuilder.Entity<User>()
                .HasOne(u => u.EnquiryComment)
                .WithOne(ec => ec.User)
                .HasForeignKey<EnquiryComment>(ec => ec.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure EnquiryComment many-to-one relationship with EnquiryFlag
            modelBuilder.Entity<EnquiryComment>()
                .HasOne(ec => ec.EnquiryFlag)
                .WithMany(ef => ef.EnquiryComments)
                .HasForeignKey(ec => ec.EnquiryFlagId)
                .OnDelete(DeleteBehavior.Restrict);

            //Configure AccessList relationships
            modelBuilder.Entity<AccessList>()
                .HasKey(al => new { al.EnquiryRequestId, al.DocumentId, al.EnquiryId });

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.EnquiryFlag)
                .WithMany(ef => ef.AccessLists)
                .HasForeignKey(al => al.EnquiryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.InstitutionEnquiryRequest)
                .WithMany(ier => ier.AccessLists)
                .HasForeignKey(al => al.EnquiryRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.Document)
                .WithMany(d => d.AccessLists)
                .HasForeignKey(al => al.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Notification one-to-many relationship with NotificationHistory
            modelBuilder.Entity<Notification>()
                .HasMany(n => n.NotificationHistories)
                .WithOne(nh => nh.Notification)
                .HasForeignKey(nh => nh.NotificationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DocumentAccess relationships (Secure Vault)
            // Use CASCADE to delete orphaned grants when document is deleted
            modelBuilder.Entity<DocumentAccess>()
                .HasOne(da => da.Document)
                .WithMany(d => d.SharedWith)
                .HasForeignKey(da => da.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocumentAccess>()
                .HasOne(da => da.GrantedToUser)
                .WithMany()
                .HasForeignKey(da => da.GrantedToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure DocumentAccessLog relationships (Audit Trail)
            // Use NO_ACTION to avoid multiple cascade paths
            modelBuilder.Entity<DocumentAccessLog>()
                .HasOne(dal => dal.Document)
                .WithMany(d => d.AccessLogs)
                .HasForeignKey(dal => dal.DocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DocumentAccessLog>()
                .HasOne(dal => dal.AccessedByUser)
                .WithMany()
                .HasForeignKey(dal => dal.AccessedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // ===== COMPLIANCE SYSTEM CONFIGURATIONS =====

            // Configure ComplianceStatus one-to-many with DocumentComplianceCheck
            modelBuilder.Entity<ComplianceStatus>()
                .HasMany(cs => cs.DocumentChecks)
                .WithOne(dc => dc.ComplianceStatus)
                .HasForeignKey(dc => dc.ComplianceStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ComplianceStatus one-to-many with ComplianceHistory
            modelBuilder.Entity<ComplianceStatus>()
                .HasMany(cs => cs.ComplianceHistories)
                .WithOne(ch => ch.ComplianceStatus)
                .HasForeignKey(ch => ch.ComplianceStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ComplianceStatus one-to-many with ComplianceAlert
            modelBuilder.Entity<ComplianceStatus>()
                .HasMany(cs => cs.ComplianceAlerts)
                .WithOne(ca => ca.ComplianceStatus)
                .HasForeignKey(ca => ca.ComplianceStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ComplianceStatus one-to-many with ComplianceAuditLog
            modelBuilder.Entity<ComplianceStatus>()
                .HasMany(cs => cs.AuditLogs)
                .WithOne(al => al.ComplianceStatus)
                .HasForeignKey(al => al.ComplianceStatusId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceStatus>()
                .HasMany(cs => cs.ComplianceResults)
                .WithOne(cr => cr.ComplianceStatus)
                .HasForeignKey(cr => cr.ComplianceStatusId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComplianceRule>()
                .HasMany(cr => cr.Requirements)
                .WithOne(r => r.ComplianceRule)
                .HasForeignKey(r => r.ComplianceRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComplianceRule>()
                .HasMany(cr => cr.Checks)
                .WithOne(c => c.ComplianceRule)
                .HasForeignKey(c => c.ComplianceRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComplianceResult>()
                .HasMany(cr => cr.Checks)
                .WithOne(c => c.ComplianceResult)
                .HasForeignKey(c => c.ComplianceResultId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User one-to-one with ComplianceStatus (optional)
            modelBuilder.Entity<User>()
                .HasOne(u => u.ComplianceStatus)
                .WithOne(cs => cs.User)
                .HasForeignKey<ComplianceStatus>(cs => cs.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Department one-to-many with ComplianceStatus
            modelBuilder.Entity<Department>()
                .HasMany(d => d.ComplianceStatuses)
                .WithOne(cs => cs.Department)
                .HasForeignKey(cs => cs.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure DocumentComplianceCheck relationships
            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasOne(dc => dc.Document)
                .WithMany()
                .HasForeignKey(dc => dc.DocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasOne(dc => dc.CheckedByUser)
                .WithMany()
                .HasForeignKey(dc => dc.CheckedBy)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasOne(dc => dc.ManualReviewedByUser)
                .WithMany()
                .HasForeignKey(dc => dc.ManuallyReviewedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasOne(dc => dc.AppliedRule)
                .WithMany()
                .HasForeignKey(dc => dc.AppliedRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ComplianceHistory relationships
            modelBuilder.Entity<ComplianceHistory>()
                .HasOne(ch => ch.ChangedByUser)
                .WithMany()
                .HasForeignKey(ch => ch.ChangedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceHistory>()
                .HasOne(ch => ch.ApprovedByUser)
                .WithMany()
                .HasForeignKey(ch => ch.ApprovedBy)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ComplianceAlert relationships
            modelBuilder.Entity<ComplianceAlert>()
                .HasOne(ca => ca.Document)
                .WithMany()
                .HasForeignKey(ca => ca.DocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceAlert>()
                .HasOne(ca => ca.User)
                .WithMany()
                .HasForeignKey(ca => ca.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceAlert>()
                .HasOne(ca => ca.AcknowledgedByUser)
                .WithMany()
                .HasForeignKey(ca => ca.AcknowledgedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceAlert>()
                .HasOne(ca => ca.ResolvedByUser)
                .WithMany()
                .HasForeignKey(ca => ca.ResolvedBy)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ComplianceAlert>()
                .HasOne(ca => ca.EscalatedToUser)
                .WithMany()
                .HasForeignKey(ca => ca.EscalatedTo)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ComplianceAuditLog relationships
            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(al => al.PerformedByUser)
                .WithMany()
                .HasForeignKey(al => al.PerformedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(al => al.Document)
                .WithMany()
                .HasForeignKey(al => al.DocumentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(al => al.CheckRecord)
                .WithMany()
                .HasForeignKey(al => al.DocumentCheckId)
                .OnDelete(DeleteBehavior.SetNull);

            // Add indexes for performance
            modelBuilder.Entity<ComplianceStatus>()
                .HasIndex(cs => cs.UserId);

            modelBuilder.Entity<ComplianceStatus>()
                .HasIndex(cs => cs.DepartmentId);

            modelBuilder.Entity<ComplianceStatus>()
                .HasIndex(cs => cs.OverallStatus);

            modelBuilder.Entity<ComplianceStatus>()
                .HasIndex(cs => cs.RiskLevel);

            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasIndex(dc => dc.DocumentId);

            modelBuilder.Entity<DocumentComplianceCheck>()
                .HasIndex(dc => dc.ComplianceStatusId);

            modelBuilder.Entity<ComplianceAlert>()
                .HasIndex(ca => ca.UserId);

            modelBuilder.Entity<ComplianceAlert>()
                .HasIndex(ca => ca.IsResolved);

            modelBuilder.Entity<ComplianceAlert>()
                .HasIndex(ca => ca.Severity);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasIndex(al => al.ComplianceStatusId);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasIndex(al => al.PerformedAt);
        }
    }
}
