using System.ComponentModel.DataAnnotations;

namespace BadgeCraft_Net.DTOs
{
    public class CreateBadgeTemplateDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = "Draft";

        public string PageSize { get; set; } = "A4";

        public int BadgesPerPage { get; set; }

        public decimal BadgeWidth { get; set; }

        public decimal BadgeHeight { get; set; }

        public string? Background { get; set; }

        public List<BadgeTemplateFieldDto> Fields { get; set; } = new();
    }
}