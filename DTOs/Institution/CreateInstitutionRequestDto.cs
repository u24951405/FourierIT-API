using FourierIT_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.DTOs.Institution
{
    public class CreateInstitutionRequestDto
    {

  
        public string InstitutionName { get; set; } = string.Empty;

       
        public string VerifiedDomain { get; set; } = string.Empty;

        public int RegNumber { get; set; }

        public int TypeId { get; set; }

        // Navigation property
        public InstitutionType InstitutionType { get; set; } = null!;
    }
}
