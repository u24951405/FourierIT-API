using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;
namespace FourierIT_API.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure UserRole many-to-many relationship
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // Configure RolePermission many-to-many relationship
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Configure User-Profile one-to-one relationship with a shared primary key
            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<Profile>(p => p.UserId);

            // Configure Address-Profile one-to-one relationship
            modelBuilder.Entity<Address>()
                .HasOne(a => a.Profile)
                .WithOne(p => p.Address)
                .HasForeignKey<Profile>(p => p.AddressId);

            // Configure Suburb one-to-many relationship with Address
            modelBuilder.Entity<Suburb>()
                .HasMany( s=> s.Addresses)
                .WithOne( a => a.Suburb)
                .HasForeignKey(a => a.SuburbID);

            // Configure Province one-to-many relationship with City
            modelBuilder.Entity<Province>()
                .HasMany(p => p.Cities)
                .WithOne(c => c.Province)
                .HasForeignKey(c => c.ProvinceId);

            // Configure City one-to-many relationship with suburb
            modelBuilder.Entity<City>()
                .HasMany(c => c.Suburbs)
                .WithOne(s => s.City)
                .HasForeignKey( s => s.CityId);

            // Configure User one-to-many relationship with document
            modelBuilder.Entity<User>()
                .HasMany(u => u.Documents)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId);

            // Configure DocumentType one-to-many relationship with document
            modelBuilder.Entity<DocumentType>()
                .HasMany(dt => dt.Documents)
                .WithOne(d => d.DocumentType)
                .HasForeignKey(d => d.DocumentTypeId);

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
                .HasForeignKey(frh => frh.RuleId);

            // Configure UserSecurityQuestion many-to-many relationship
            modelBuilder.Entity<UserSecurityQuestion>()
                .HasKey(usq => new { usq.UserId, usq.SecurityQuestionId });

            modelBuilder.Entity<UserSecurityQuestion>()
                .HasOne(usq => usq.User)
                .WithMany(u => u.UserSecurityQuestions)
                .HasForeignKey(usq => usq.UserId);

            modelBuilder.Entity<UserSecurityQuestion>()
                .HasOne(usq => usq.SecurityQuestion)
                .WithMany(sq => sq.UserSecurityQuestions)
                .HasForeignKey(usq => usq.SecurityQuestionId);

            // Configure UserNotification many-to-many relationship
            modelBuilder.Entity<UserNotification>()
                .HasKey(un => new { un.NotificationId, un.UserId });

            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.Notification)
                .WithMany(n => n.UserNotifications)
                .HasForeignKey(un => un.NotificationId);

            modelBuilder.Entity<UserNotification>()
                .HasOne(un => un.User)
                .WithMany(u => u.UserNotifications)
                .HasForeignKey(un => un.UserId);

            // Configure document one-to-one relationship with DocumentBlob
            modelBuilder.Entity<Document>()
                .HasOne(d => d.DocumentBlob)
                .WithOne(db => db.Document)
                .HasForeignKey<DocumentBlob>(db => db.DocumentId);

            // Configure DocumentBlob one-to-many relationship with BlobHistory
            modelBuilder.Entity<DocumentBlob>()
                .HasMany(db => db.BlobHistories)
                .WithOne(bh => bh.DocumentBlob)
                .HasForeignKey(bh => bh.DocumentBlobId);

            // Configure Document one-to-many relationship with CertificationDetails
            modelBuilder.Entity<Document>()
                .HasMany(d => d.CertificationDetails)
                .WithOne(cd => cd.Document)
                .HasForeignKey(cd => cd.DocumentId);

            // Configure Document one-to-many relationship with DocumentStatusHistory
            modelBuilder.Entity<Document>()
                .HasMany(d => d.DocumentStatusHistories)
                .WithOne(dsh => dsh.Document)
                .HasForeignKey(dsh => dsh.DocumentId);

            // Configure InstitutionType one-to-many with Institution
            modelBuilder.Entity<InstitutionType>()
                .HasMany(it => it.Institutions)
                .WithOne(i => i.InstitutionType)
                .HasForeignKey(i => i.TypeId);

            // Many-to-many relationship for InstitutionMembers
            modelBuilder.Entity<InstitutionMembers>()
                .HasKey(im => new { im.MembersId, im.UserId, im.InstitutionId });

            modelBuilder.Entity<InstitutionMembers>()
                .HasOne(im => im.Institution)
                .WithMany(i => i.InstitutionMembers)
                .HasForeignKey(im => im.InstitutionId);

            modelBuilder.Entity<InstitutionMembers>()
                .HasOne(im => im.User)
                .WithMany(u => u.InstitutionMembers)
                .HasForeignKey(im => im.UserId);

            // Configure Institution One-to-many relationship with Branch
            modelBuilder.Entity<Institution>()
                .HasMany(i => i.Branches)
                .WithOne(b => b.Institution)
                .HasForeignKey(b => b.InstitutionId);

            // Configure Branch one-to-many relationship with Department
            modelBuilder.Entity<Branch>()
                .HasMany(b => b.Departments)
                .WithOne(d => d.Branch)
                .HasForeignKey(d => d.BranchId);

            // Configure ClientEnlistment relationships
            modelBuilder.Entity<ClientEnlistment>()
                .HasKey(ce => new { ce.ClientEnlistmentId, ce.UserId, ce.InstitutionId });

            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.User)
                .WithMany(u => u.ClientEnlistments)
                .HasForeignKey(ce => ce.UserId);

            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.Institution)
                .WithMany(i => i.ClientEnlistments)
                .HasForeignKey(ce => ce.InstitutionId);

            // Configure ClientEnlistment one-to-one relationship with ClientRiskRating
            modelBuilder.Entity<ClientEnlistment>()
                .HasOne(ce => ce.ClientRiskRating)
                .WithOne(crr => crr.ClientEnlistment)
                .HasForeignKey<ClientRiskRating>(crr => crr.ClientEnlistmentId);

            // Configure RiskRatingVariable relationships
            modelBuilder.Entity<RiskRatingVariable>()
                .HasKey(rrv => new { rrv.RatingId, rrv.RiskVariableId });

            modelBuilder.Entity<RiskRatingVariable>()
                .HasOne(rrv => rrv.ClientRiskRating)
                .WithMany(crr => crr.RiskRatingVariables)
                .HasForeignKey(rrv => rrv.RatingId);

            modelBuilder.Entity<RiskRatingVariable>()
                .HasOne(rrv => rrv.RiskVariable)
                .WithMany(rv => rv.RiskRatingVariables)
                .HasForeignKey(rrv => rrv.RiskVariableId);

            // Configure ClientRiskRating one-to-many relationship with RiskHistory
            modelBuilder.Entity<ClientRiskRating>()
                .HasMany(crr => crr.RiskHistories)
                .WithOne(rh => rh.ClientRiskRating)
                .HasForeignKey(rh => rh.ClientRiskRatingId);

            // Configure Institution one-to-many relationship with InstitutionEnquiryRequest
            modelBuilder.Entity<Institution>()
                .HasMany(i => i.institutionEnquiryRequests)
                .WithOne(ier => ier.Institution)
                .HasForeignKey(ier => ier.InstitutionId);

            // Configure InstitutionEnquiryRequest one-to-one relationship with AccessToken
            modelBuilder.Entity<InstitutionEnquiryRequest>()
                .HasOne(ier => ier.AccessToken)
                .WithOne(at => at.institutionEnquiryRequest)
                .HasForeignKey<AccessToken>(at => at.EnquiryRequestId);

            // Configure AccessToken one-to-one relationship with EnquirySession
            modelBuilder.Entity<AccessToken>()
                .HasOne(at => at.EnquirySession)
                .WithOne(es => es.AccessToken)
                .HasForeignKey<EnquirySession>(es => es.TokenId);

            // Configure AccessToken one-to-one relationship with User **
            modelBuilder.Entity<AccessToken>()
                .HasOne(at => at.User)
                .WithOne(u => u.AccessToken)
                .HasForeignKey<AccessToken>(at => at.UserId);

            // Configure User one-to-one relationship with EnquiryComment
            modelBuilder.Entity<User>()
                .HasOne(u => u.EnquiryComment)
                .WithOne(ec => ec.User)
                .HasForeignKey<EnquiryComment>(ec => ec.UserId);

            // Configure EnquiryComment many-to-one relationship with EnquiryFlag
            modelBuilder.Entity<EnquiryComment>()
                .HasOne(ec => ec.EnquiryFlag)
                .WithMany(ef => ef.EnquiryComments)
                .HasForeignKey(ec => ec.EnquiryFlagId);

            //Configure AccessList relationships
            modelBuilder.Entity<AccessList>()
                .HasKey(al => new { al.EnquiryRequestId, al.DocumentId, al.EnquiryId });

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.EnquiryFlag)
                .WithOne(ef => ef.AccessList)
                .HasForeignKey<AccessList>(al => al.EnquiryId);

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.InstitutionEnquiryRequest)
                .WithMany(ier => ier.AccessLists)
                .HasForeignKey(al => al.EnquiryRequestId);

            modelBuilder.Entity<AccessList>()
                .HasOne(al => al.Document)
                .WithMany(d => d.AccessLists)
                .HasForeignKey(al => al.DocumentId);

            // Configure Notification one-to-many relationship with NotificationHistory
            modelBuilder.Entity<Notification>()
                .HasMany(n => n.NotificationHistories)
                .WithOne(nh => nh.Notification)
                .HasForeignKey(nh => nh.NotificationId);
        }
    }
}
