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

        //Start of the session, when the user starts the enquiry process
        public TimeOnly SessionStartTime { get; set; } = TimeOnly.FromDateTime(DateTime.Now);

        //End of the session, when the user finishes the enquiry process
        public TimeOnly SessionEndTime { get; set; } = TimeOnly.FromDateTime(DateTime.Now);
    }
}
