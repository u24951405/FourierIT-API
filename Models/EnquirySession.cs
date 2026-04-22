using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourierIT_API.Models
{
    public class EnquirySession
    {
        [Key]
        public int EnquirySessionId { get; set; }

        [ForeignKey("AccessToken")]
        public int TokenId { get; set; }

        public AccessToken AccessToken { get; set; } = null!;

        public TimeOnly SessionStartTime { get; set; } 
        public TimeOnly SessionEndTime { get; set; }
    }
}
