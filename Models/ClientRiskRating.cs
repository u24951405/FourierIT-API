using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class ClientRiskRating
    {
        [Key]
        public int RatingId { get; set; }

        [ForeignKey("ClientEnlistment")]
        public int ClientEnlistmentId { get; set; }
        public ClientEnlistment ClientEnlistment { get; set; } = null!;

        [Required]
        public int TotalScore { get; set; }

        [Required]
        public int RiskLevel { get; set; }

        public DateOnly LastUpdated { get; set; } = new DateOnly();

        public ICollection<RiskRatingVariable> RiskRatingVariables { get; set; } = new List<RiskRatingVariable>();

    }
}
