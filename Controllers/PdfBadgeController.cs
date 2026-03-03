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
    private readonly BadgePdfService _badgePdfService;

    public PdfBadgeController(
        AppDbContext context,
        BadgePdfService badgePdfService)
    {
        _context = context;
        _badgePdfService = badgePdfService;
    }

    [HttpGet("generate/{jobId}")]
    public async Task<IActionResult> GeneratePdf(int jobId)
    {
        var job = await _context.UploadJobs
            .Include(j => j.Template)
            .ThenInclude(t => t.Fields)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null) return NotFound("Job not found");

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

        var filePath = _badgePdfService.GenerateBadgesPdf(job.Template, records);

        job.Status = "PdfGenerated";
        await _context.SaveChangesAsync();

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var fileName = Path.GetFileName(filePath);

        return File(fileBytes, "application/pdf", fileName);
    }
}