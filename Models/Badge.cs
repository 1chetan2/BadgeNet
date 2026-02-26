using System.ComponentModel.DataAnnotations;

namespace BadgeCraft_Net.Models
{
    public class Badge
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string BgColor { get; set; }

        public string TextColor { get; set; }

        public string ImageUrl { get; set; }

        public int OrganizationId { get; set; }

        public int CreatedBy { get; set; }
    }
}