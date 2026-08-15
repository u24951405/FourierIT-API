using System.ComponentModel.DataAnnotations;

namespace FourierIT_API.Models
{
    public class RequiredDocument
    {
        [Key]
        public int RequiredDocumentId { get; set; }

        public int EntityTypeId { get; set; }
        public EntityType EntityType { get; set; } = null!;

        public int DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;

        public bool IsMandatory { get; set; } = true;
        public string? Description { get; set; }
    }
}
