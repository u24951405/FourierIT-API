using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class RiskVariable
    {
        [Key]
        public int RiskVariableId { get; set; }

        public string VarName { get; set; } = string.Empty;

        public string VarDescription { get; set; } = string.Empty;

        public decimal weightMultiplier { get; set; }

        public ICollection<RiskRatingVariable> RiskRatingVariables { get; set; } = new List<RiskRatingVariable>();
    }
}
