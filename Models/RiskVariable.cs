using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace FourierIT_API.Models
{
    public class RiskVariable
    {
        [Key]
        public int RiskVariableId { get; set; }

        [Required]
        [StringLength(100)]
        public string VarName { get; set; } = string.Empty;

        [Required]
        public decimal WeightMultiplier { get; set; }

        public ICollection<RiskRatingVariable> RiskRatingVariables { get; set; } = new List<RiskRatingVariable>();
    }
}
