using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.DTOs.Document
{
    /// <summary>An institution asking to keep approved access for longer.</summary>
    public class RequestAccessExtensionDto
    {
        /// <summary>Extra days of access wanted (up to the "Longest access extension" setting).</summary>
        [Range(1, 30)]
        public int Days { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>The owner's answer to an extension request, or a note about how a flag was dealt with.</summary>
    public class FollowUpNoteDto
    {
        [StringLength(500)]
        public string? Note { get; set; }
    }
}
