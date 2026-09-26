namespace FourierIT_API.DTOs.Document
{
    public class EntityTypeHierarchyDto
    {
        public int EntityTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<DocumentTypeHierarchyDto> DocumentTypes { get; set; } = new();
        public bool UserHasAccess { get; set; } = true;
    }

    public class DocumentTypeHierarchyDto
    {
        public int DocumentTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public List<DocumentItemDto> Documents { get; set; } = new();
    }

    public class DocumentItemDto
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsCertified { get; set; }
        public DateTime UploadedDate { get; set; }
        public long FileSizeBytes { get; set; }

        // Permission flags
        public bool UserCanView { get; set; } = true;
        public bool UserCanDownload { get; set; }
        public bool UserCanEdit { get; set; }
        public bool UserCanDelete { get; set; }
        public bool UserCanShare { get; set; }
    }

    public class SearchResultDto
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentTypeName { get; set; } = string.Empty;
        public string EntityTypeName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsCertified { get; set; }
        public DateTime UploadedDate { get; set; }
        public long FileSizeBytes { get; set; }
        public bool UserCanView { get; set; }
        public bool UserCanDownload { get; set; }
        public bool UserCanDelete { get; set; }
    }
}
