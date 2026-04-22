using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class Institution
    {
        [Key]
        public int InstitutionId { get; set; }

        [Required]
        public string InstitutionName { get; set; } = string.Empty;

        [Required]
        public string VerifiedDomain { get; set; } = string.Empty;

        [Required]
        public int RegNumber { get; set; }

        [ForeignKey("InstitutionType")]
        public int TypeId { get; set; }

        // Navigation property
        public InstitutionType InstitutionType { get; set; } = null!;

        public ICollection<InstitutionMembers> InstitutionMembers { get; set; } = new List<InstitutionMembers>();

        public ICollection<Branch> Branches { get; set; } = new List<Branch>();

        public ICollection<ClientEnlistment> ClientEnlistments { get; set; } = new List<ClientEnlistment>();
    }
}
