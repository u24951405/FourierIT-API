using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.User
{
    public class VerifyEntityRequestDto
    {
        [Required]
        public int EntityTypeId { get; set; }

        [Required]
        public string IdentificationNumber { get; set; } = string.Empty;
    }
}
