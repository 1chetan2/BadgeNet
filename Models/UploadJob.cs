using BadgeCraft_Net.Models;

public class UploadJob
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public int TemplateId { get; set; }

    public string CsvPath { get; set; }

    public string? MappingJson { get; set; }

    public string? Status { get; set; } // Processing | Completed | Failed

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public GeneratedDocument GeneratedDocument { get; set; }
}