using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.Institution
{
    public class UpdateInstitutionRequestDto
    {
        [Required]
        [StringLength(150)]
        public string InstitutionName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string VerifiedDomain { get; set; } = string.Empty;

        [Required]
        public int RegNumber { get; set; }

        [Required]
        public int TypeId { get; set; }
    }
}
