using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class CertificationDetails
    {
        [Key]
        public string CertificationID { get; set; } = string.Empty;

        [Required]
        public string CommissionerName { get; set; } = string.Empty;
        public DateOnly CertificationDate { get; set; } = new DateOnly();

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;
    }
}
