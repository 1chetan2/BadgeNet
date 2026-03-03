using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BadgeCraft_Net.Models
{
    public class BadgeTemplate
    {
        [Key]
        public int Id { get; set; }  // PDF sketch mein TemplateId, lekin Id common hai

        [Required(ErrorMessage = "Template name is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Name must be 3-200 characters")]
        public string Name { get; set; } = string.Empty;  // PDF: TemplateName

        [Required(ErrorMessage = "Status is required")]
        [RegularExpression("Draft|Published", ErrorMessage = "Status must be Draft or Published")]
        public string Status { get; set; } = "Draft";

        [Required]
        [StringLength(10)]
        public string PageSize { get; set; } = "A4";  // Fixed for MVP

        [Range(1, 100, ErrorMessage = "Badges per page must be between 1 and 100")]
        public int BadgesPerPage { get; set; } = 6;

        [Required(ErrorMessage = "Badge width is required")]
        [Range(10, 210, ErrorMessage = "Width should be realistic for A4 (mm)")]
        [Column(TypeName = "decimal(6,2)")]
        public decimal BadgeWidth { get; set; }  // mm

        [Required(ErrorMessage = "Badge height is required")]
        [Range(10, 297, ErrorMessage = "Height should be realistic for A4 (mm)")]
        [Column(TypeName = "decimal(6,2)")]
        public decimal BadgeHeight { get; set; } // mm

        [StringLength(500)]
        public string? Background { get; set; }  // color hex or image URL/path

        // Tenant isolation (mandatory)
        [Required]
        public int OrganizationId { get; set; }

        [ForeignKey("OrganizationId")]
        [JsonIgnore]
        public Organization Organization { get; set; } = null!;

        // Navigation: one template → many fields
        public List<BadgeTemplateField> Fields { get; set; } = new List<BadgeTemplateField>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}