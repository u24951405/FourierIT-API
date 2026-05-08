using FourierIT_API.Models;

namespace FourierIT_API.DTOs.Institution
{
    public class UpdateInstitutionRequestDto
    {
        public string InstitutionName { get; set; } = string.Empty;


        public string VerifiedDomain { get; set; } = string.Empty;

        public int RegNumber { get; set; }

        public int TypeId { get; set; }

        // Navigation property
        public InstitutionType InstitutionType { get; set; } = null!;
    }
}
