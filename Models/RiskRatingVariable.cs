using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class RiskRatingVariable
    {
        [Key]
        [ForeignKey("ClientRiskRating")]
        public int RatingId { get; set; }
        public ClientRiskRating ClientRiskRating { get; set; } = null!;
    }
}
