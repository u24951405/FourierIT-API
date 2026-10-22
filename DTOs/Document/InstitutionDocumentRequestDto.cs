namespace FourierIT_API.DTOs.Document
{
    public class InstitutionDocumentRequestDto
    {
        /// <summary>
        /// Purpose of the document request (required)
        /// </summary>
        public string PurposeNote { get; set; } = string.Empty;

        /// <summary>
        /// Type of request: "Department" or "Individual" (default: "Individual")
        /// </summary>
        public string RequestType { get; set; } = "Individual";

        /// <summary>
        /// Target Department ID - required when RequestType is "Department"
        /// </summary>
        public int? TargetDepartmentId { get; set; }

        /// <summary>
        /// Target User ID - required when RequestType is "Individual"
        /// </summary>
        public string? TargetUserId { get; set; }

        /// <summary>
        /// List of documents being requested
        /// </summary>
        public List<RequestedDocumentTypeDto> RequestedDocuments { get; set; } = new();

        /// <summary>
        /// Optional deadline for submission of the requested documents.
        /// </summary>
        public DateTimeOffset? SubmissionDeadline { get; set; }

        // There is no ReferenceNumber here: the system generates it when the request is created.
    }

    public class UpdateInstitutionDocumentRequestDto
    {
        public string PurposeNote { get; set; } = string.Empty;
        public List<RequestedDocumentTypeDto> RequestedDocuments { get; set; } = new();
        public DateTimeOffset? SubmissionDeadline { get; set; }
    }
}
