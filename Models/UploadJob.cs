using BadgeCraft_Net.Models;

namespace BadgeCraft_Net.Models
{
    public class UploadJob
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        // Foreign Key
        public int TemplateId { get; set; }

        public string CsvPath { get; set; } = string.Empty;

        public string? MappingJson { get; set; }

        public string? Status { get; set; }

        public string? ErrorMessage { get; set; }

        //public DateTime CreatedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public int CreatedBy { get; set; }

        // Navigation Properties
        public BadgeTemplate? Template { get; set; }
        public GeneratedDocument? GeneratedDocument { get; set; }
    }
}