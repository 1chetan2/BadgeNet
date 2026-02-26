using BadgeCraft_Net.Data;
using BadgeCraft_Net.Models;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace BadgeCraft_Net.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CsvController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CsvController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(
     IFormFile file,
     [FromForm] int templateId
 )
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Validate template exists
            var templateExists = await _context.BadgeTemplates
                .AnyAsync(t => t.Id == templateId);

            if (!templateExists)
                return BadRequest("Invalid TemplateId");

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Uploads"
            );

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + ".csv";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var job = new UploadJob
            {
                OrganizationId = 1,
                TemplateId = templateId,
                CsvPath = filePath,
                Status = "Uploaded",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = 1 // replace with logged-in user id
            };

            _context.UploadJobs.Add(job);
            await _context.SaveChangesAsync();

            return Ok(new { JobId = job.Id });
        }
        // Preview First 5 Rows
        [HttpGet("{jobId}/preview")]
        public async Task<IActionResult> Preview(int jobId)
        {
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null)
                return NotFound();

            var records = new List<Dictionary<string, string>>();

            using var reader = new StreamReader(job.CsvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var rows = csv.GetRecords<dynamic>().Take(5);

            foreach (var row in rows)
            {
                var dict = new Dictionary<string, string>();
                foreach (var prop in (IDictionary<string, object>)row)
                {
                    dict[prop.Key] = prop.Value?.ToString();
                }
                records.Add(dict);
            }
                
            return Ok(records);
        }

        //Get CSV Columns
        [HttpGet("{jobId}/columns")]
        public async Task<IActionResult> GetColumns(int jobId)
        {
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null)
                return NotFound();

            using var reader = new StreamReader(job.CsvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();
            return Ok(csv.HeaderRecord);
        }

        // Save Mapping     
        [HttpPost("{jobId}/mapping")]                   
        public async Task<IActionResult> SaveMapping(int jobId, [FromBody] Dictionary<string, string> mapping)
        {                   
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null)
                return NotFound();

            job.MappingJson = JsonSerializer.Serialize(mapping);
            job.Status = "Mapped";

            await _context.SaveChangesAsync();
            return Ok("Mapping saved");
        }

            [HttpPost("generate/{jobId}")]
            public async Task<IActionResult> Generate(int jobId)
            {
                var job = await _context.UploadJobs
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                if (job == null)
                    return NotFound("Job not found.");

                if (string.IsNullOrEmpty(job.MappingJson))
                    return BadRequest("Mapping not saved.");

                var mapping = JsonSerializer
                    .Deserialize<Dictionary<string, string>>(job.MappingJson);

                if (mapping == null)
                    return BadRequest("Invalid mapping.");

                // Validate Title mapping exists
                if (!mapping.Any(x => x.Value == "Title"))
                    return BadRequest("Title field not mapped.");

                var titleColumn = mapping
                    .First(x => x.Value == "Title").Key;

                var subtitleColumn = mapping
                    .FirstOrDefault(x => x.Value == "Subtitle").Key;

                var generatedBadges = new List<object>();

                using var reader = new StreamReader(job.CsvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                var records = csv.GetRecords<dynamic>().ToList();

                foreach (var row in records)
                {
                    var dict = (IDictionary<string, object>)row;

                    var title = dict.ContainsKey(titleColumn)
                        ? dict[titleColumn]?.ToString()
                        : "";

                    var subtitle = (!string.IsNullOrEmpty(subtitleColumn) &&
                                    dict.ContainsKey(subtitleColumn))
                        ? dict[subtitleColumn]?.ToString()
                        : "";

                    generatedBadges.Add(new
                    {
                        Title = title,
                        Subtitle = subtitle
                    });
                }

                job.Status = "Completed";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Badges generated successfully",
                    Count = generatedBadges.Count,
                    Data = generatedBadges
                });
            }
        }
    }

                                        