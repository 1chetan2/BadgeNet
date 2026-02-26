using BadgeCraft_Net.Data;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text.Json;

[Route("api/[controller]")]
[ApiController]
public class PdfBadgeController : ControllerBase
{
    private readonly AppDbContext _context;

    public PdfBadgeController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("generate/{jobId}")]
    public async Task<IActionResult> GeneratePdf(int jobId)
    {
        var job = await _context.UploadJobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null) return NotFound("Job not found");
        if (string.IsNullOrEmpty(job.MappingJson)) return BadRequest("Mapping not saved");

        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(job.MappingJson);
        if (!mapping.Any(x => x.Value == "Title")) return BadRequest("Title field not mapped");

        var titleColumn = mapping.First(x => x.Value == "Title").Key;
        var subtitleColumn = mapping.FirstOrDefault(x => x.Value == "Subtitle").Key;

        var records = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(job.CsvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        foreach (var row in csv.GetRecords<dynamic>())
        {
            var dict = new Dictionary<string, string>();
            foreach (var prop in (IDictionary<string, object>)row)
                dict[prop.Key] = prop.Value?.ToString() ?? "";
            records.Add(dict);
        }

        // Generate PDF with QuestPDF
        var pdfBytes = QuestPDF.Fluent.Document.Create(container =>
        {
            int count = 0;
            container.Page(page =>
            {
                page.Margin(20);
                page.Content().Column(col =>
                {
                    foreach (var r in records)
                    {
                        var title = r.ContainsKey(titleColumn) ? r[titleColumn] : "";
                        var subtitle = (!string.IsNullOrEmpty(subtitleColumn) && r.ContainsKey(subtitleColumn)) ? r[subtitleColumn] : "";

                        col.Item().Height(150).Padding(10).Background(Colors.Grey.Lighten3).AlignMiddle().AlignCenter().Column(c =>
                        {
                            c.Item().Text(title).SemiBold().FontSize(16).AlignCenter();
                            c.Item().Text(subtitle).FontSize(12).AlignCenter();
                        });

                        count++;
                        if (count % 6 == 0)
                        {
                            col.Item().PageBreak();
                        }
                    }
                });
            });
        }).GeneratePdf();

        job.Status = "PdfGenerated";
        await _context.SaveChangesAsync();

        return File(pdfBytes, "application/pdf", "Badges.pdf");
    }
}