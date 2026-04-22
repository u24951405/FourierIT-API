using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class RiskHistory
    {
        [Key]
        public int RiskHistoryId { get; set; }

        [ForeignKey("ClientRiskRating")]
        public int ClientRiskRatingId { get; set; }
        public ClientRiskRating ClientRiskRating { get; set; } = null!;
    }
}
