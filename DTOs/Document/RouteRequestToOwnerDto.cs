namespace FourierIT_API.DTOs.Document
{
    /// <summary>
    /// DTO for department admin to route a request to a specific document owner
    /// </summary>
    public class RouteRequestToOwnerDto
    {
        /// <summary>
        /// The user ID of the document owner to route this request to
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Optional note from the department admin
        /// </summary>
        public string? AdminNote { get; set; }
    }
}
