using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class CertificationDetails
    {
        [Key]
        public string CertificationID { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string CommissionerName { get; set; } = string.Empty;

        public DateTimeOffset CertificationDate { get; set; } = DateTimeOffset.Now;

        [ForeignKey("Document")]
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;
    }
}
