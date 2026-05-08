using FourierIT_API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.DTOs.Institution
{
    public class InstitutionDto
    {


    
        public int InstitutionId { get; set; }

      
        public string InstitutionName { get; set; } = string.Empty;

        public string VerifiedDomain { get; set; } = string.Empty;

       
        public int RegNumber { get; set; }

    
        public int TypeId { get; set; }

        public InstitutionType InstitutionType { get; set; } = null!;
    }
}
