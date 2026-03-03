namespace BadgeCraft_Net.Models
{
    public class GeneratedDocument
    {
        public int Id { get; set; }

        public int UploadJobId { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ADD THIS navigation property
        public UploadJob UploadJob { get; set; }
    }
}