using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BadgeCraft_Net.Models
{
    public class Badge
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BadgeTemplateId { get; set; }

        [ForeignKey("BadgeTemplateId")]
        public BadgeTemplate BadgeTemplate { get; set; } = null!;

        [Required]
        public string DataJson { get; set; } = "{}";

        [Required]
        public int OrganizationId { get; set; }

        public int CreatedBy { get; set; }

        public string? PdfPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}