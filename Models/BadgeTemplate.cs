using System.ComponentModel.DataAnnotations;

namespace BadgeCraft_Net.Models
{
    public class BadgeTemplate
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        // Organization Isolation
        public int OrganizationId { get; set; }
    }
}