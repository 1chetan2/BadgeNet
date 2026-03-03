using System.ComponentModel.DataAnnotations;

namespace BadgeCraft_Net.Models
{
    public class Organization
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties

        public List<User> Users { get; set; } = new();

        public List<BadgeTemplate> BadgeTemplates { get; set; } = new();

        public List<UploadJob> UploadJobs { get; set; } = new();

        public List<GeneratedDocument> GeneratedDocuments { get; set; } = new();
    }
}