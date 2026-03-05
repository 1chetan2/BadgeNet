using BadgeCraft_Net.Data;
using BadgeCraft_Net.Models;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace BadgeCraft_Net.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CsvController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly BadgePdfService _badgePdfService;

        public CsvController(AppDbContext context, BadgePdfService badgePdfService)
        {
            _context = context;
            _badgePdfService = badgePdfService;
        }

        private int GetOrgId()
        {
            var claim = User.Claims.FirstOrDefault(c => 
                c.Type == "OrganizationId" || 
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/organizationid");
            
            if (claim == null)
                throw new UnauthorizedAccessException("OrganizationId missing in claims.");

            return int.Parse(claim.Value);
        }

        private int GetUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => 
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || 
                c.Type == "sub" ||
                c.Type == "id" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Admin") || 
                   User.IsInRole("OrganizationAdmin") || 
                   User.IsInRole("OrgAdmin");
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file, [FromForm] int templateId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.OrganizationId == orgId);

            if (template == null)
                return BadRequest("Invalid TemplateId or not authorized");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + ".csv";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var job = new UploadJob
            {
                OrganizationId = orgId,
                TemplateId = templateId,
                CsvPath = filePath,
                Status = "Uploaded",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetUserId()
            };

            _context.UploadJobs.Add(job);
            await _context.SaveChangesAsync();

            return Ok(new { JobId = job.Id });
        }

        [HttpGet("job-info/{jobId}")]
        public async Task<IActionResult> GetJob(int jobId)
        {
            var orgId = GetOrgId();
            var job = await _context.UploadJobs.FindAsync(jobId);

            if (job == null) return NotFound("Job not found");
            if (job.OrganizationId != orgId) return Forbid();

            await _context.Entry(job).Reference(j => j.Template).LoadAsync();
            if (job.Template != null)
                await _context.Entry(job.Template).Collection(t => t.Fields).LoadAsync();

            return Ok(job);
        }

        [HttpGet("{jobId}/preview")]
        public async Task<IActionResult> Preview(int jobId)
        {
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null) return NotFound();

            using var reader = new StreamReader(job.CsvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var rows = csv.GetRecords<dynamic>().Take(5);
            var records = new List<Dictionary<string, string>>();

            foreach (var row in rows)
            {
                var dict = new Dictionary<string, string>();
                foreach (var prop in (IDictionary<string, object>)row)
                    dict[prop.Key] = prop.Value?.ToString() ?? "";
                records.Add(dict);
            }

            return Ok(records);
        }

        [HttpPost("{jobId}/mapping")]
        public async Task<IActionResult> SaveMapping(int jobId, [FromBody] Dictionary<string, string> mapping)
        {
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.MappingJson = JsonSerializer.Serialize(mapping);
            job.Status = "Mapped";
            await _context.SaveChangesAsync();

            return Ok("Mapping saved");
        }

        [HttpPost("{jobId}/generate")]
        public async Task<IActionResult> GeneratePdf(int jobId)
        {
            try
            {
                var job = await _context.UploadJobs
                    .Include(j => j.Template)
                        .ThenInclude(t => t.Fields)
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                if (job == null || job.Template == null || string.IsNullOrEmpty(job.MappingJson))
                    return BadRequest("Job, Template or Mapping missing");

                var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(job.MappingJson);
                if (mapping == null) return BadRequest("Invalid mapping");

                var rawRecords = new List<Dictionary<string, string>>();
                using var reader = new StreamReader(job.CsvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                foreach (var row in csv.GetRecords<dynamic>())
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in (IDictionary<string, object>)row)
                        dict[prop.Key?.Trim() ?? ""] = prop.Value?.ToString() ?? "";
                    
                    rawRecords.Add(dict);
                }

                var mappedRecords = new List<Dictionary<string, string>>();
                foreach (var raw in rawRecords)
                {
                    var mappedDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in mapping)
                    {
                        if (!string.IsNullOrEmpty(m.Value) && raw.TryGetValue(m.Value.Trim(), out var value))
                            mappedDict[m.Key] = value;
                    }
                    mappedRecords.Add(mappedDict);

                    _context.Badges.Add(new Badge
                    {
                        BadgeTemplateId = job.TemplateId,
                        OrganizationId = job.OrganizationId,
                        DataJson = JsonSerializer.Serialize(mappedDict),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = job.CreatedBy
                    });
                }

                var filePath = _badgePdfService.GenerateBadgesPdf(job.Template, mappedRecords);

                var existingDoc = await _context.GeneratedDocuments
                    .FirstOrDefaultAsync(d => d.UploadJobId == job.Id);

                if (existingDoc != null)
                {
                    existingDoc.FilePath = filePath;
                    existingDoc.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.GeneratedDocuments.Add(new GeneratedDocument
                    {
                        UploadJobId = job.Id,
                        FilePath = filePath,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                job.Status = "Completed";
                await _context.SaveChangesAsync();
                return Ok(new { Message = "PDF generated", FilePath = filePath });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Generation failed: {ex.Message}" });
            }
        }

        [HttpGet("history")]
        [HttpGet("/api/jobs")]
        public async Task<IActionResult> JobHistory()
        {
            var orgId = GetOrgId();
            var userId = GetUserId();
            var isAdmin = IsAdmin();

            var query = _context.UploadJobs
                .Include(j => j.Template)
                .Include(j => j.GeneratedDocument)
                .Where(j => j.OrganizationId == orgId);

            if (!isAdmin)
                query = query.Where(j => j.CreatedBy == userId);

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new
                {
                    j.Id,
                    TemplateName = j.Template.Name,
                    j.Status,
                    CreatedAt = j.CreatedAt,
                    PdfFile = j.GeneratedDocument != null ? j.GeneratedDocument.FilePath : null,
                    j.ErrorMessage
                })
                .ToListAsync();

            return Ok(jobs);
        }

        [AllowAnonymous]
        [HttpGet("{jobId}/download")]
        [HttpGet("/api/jobs/{jobId}/download")]
        public async Task<IActionResult> DownloadPdf(int jobId, [FromQuery] bool inline = false)
        {
            var job = await _context.UploadJobs
                .Include(j => j.GeneratedDocument)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null || job.GeneratedDocument == null) return NotFound();

            var filePath = job.GeneratedDocument.FilePath;
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);

            return inline ? File(fileBytes, "application/pdf") : File(fileBytes, "application/pdf", fileName);
        }
    }
}