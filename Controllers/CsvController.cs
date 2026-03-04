//using BadgeCraft_Net.Data;
//using BadgeCraft_Net.Models;
//using CsvHelper;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Globalization;
//using System.Text.Json;

//namespace BadgeCraft_Net.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class CsvController : ControllerBase
//    {
//        private readonly AppDbContext _context;

//        public CsvController(AppDbContext context)
//        {
//            _context = context;
//        }

//        // Upload CSV
//        [HttpPost("upload")]
//        public async Task<IActionResult> UploadCsv(
//            IFormFile file,
//            [FromForm] int templateId
//        )
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest("No file uploaded.");

//            var templateExists = await _context.BadgeTemplates
//                .AnyAsync(t => t.Id == templateId);

//            if (!templateExists)
//                return BadRequest("Invalid TemplateId");

//            var uploadsFolder = Path.Combine(
//                Directory.GetCurrentDirectory(),
//                "Uploads"
//            );

//            if (!Directory.Exists(uploadsFolder))
//                Directory.CreateDirectory(uploadsFolder);

//            var fileName = Guid.NewGuid() + ".csv";
//            var filePath = Path.Combine(uploadsFolder, fileName);

//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                await file.CopyToAsync(stream);
//            }

//            var job = new UploadJob
//            {
//                OrganizationId = 1, // later from claims
//                TemplateId = templateId,   // ✅ FIXED
//                CsvPath = filePath,
//                Status = "Uploaded",
//                CreatedAt = DateTime.UtcNow,
//                CreatedBy = 1
//            };

//            _context.UploadJobs.Add(job);
//            await _context.SaveChangesAsync();

//            return Ok(new { JobId = job.Id });
//        }

//        // Preview first 5 rows
//        [HttpGet("{jobId}/preview")]
//        public async Task<IActionResult> Preview(int jobId)
//        {
//            var job = await _context.UploadJobs.FindAsync(jobId);
//            if (job == null)
//                return NotFound();

//            var records = new List<Dictionary<string, string>>();

//            using var reader = new StreamReader(job.CsvPath);
//            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

//            var rows = csv.GetRecords<dynamic>().Take(5);

//            foreach (var row in rows)
//            {
//                var dict = new Dictionary<string, string>();
//                foreach (var prop in (IDictionary<string, object>)row)
//                    dict[prop.Key] = prop.Value?.ToString() ?? "";

//                records.Add(dict);
//            }

//            return Ok(records);
//        }

//        // Get CSV Columns
//        [HttpGet("{jobId}/columns")]
//        public async Task<IActionResult> GetColumns(int jobId)
//        {
//            var job = await _context.UploadJobs.FindAsync(jobId);
//            if (job == null)
//                return NotFound();

//            using var reader = new StreamReader(job.CsvPath);
//            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

//            csv.Read();
//            csv.ReadHeader();

//            return Ok(csv.HeaderRecord);
//        }

//        // Save Mapping
//        [HttpPost("{jobId}/mapping")]
//        public async Task<IActionResult> SaveMapping(
//            int jobId,
//            [FromBody] Dictionary<string, string> mapping)
//        {
//            var job = await _context.UploadJobs.FindAsync(jobId);
//            if (job == null)
//                return NotFound();

//            job.MappingJson = JsonSerializer.Serialize(mapping);
//            job.Status = "Mapped";

//            await _context.SaveChangesAsync();
//            return Ok("Mapping saved");
//        }
//    }
//}


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

        // ------------------------
        // 0. Diagnostics
        // ------------------------
        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { Message = "CsvController is alive", Time = DateTime.Now });

        [HttpGet("debug-list")]
        public async Task<IActionResult> DebugList()
        {
            var jobs = await _context.UploadJobs.OrderByDescending(j => j.CreatedAt).Take(20).ToListAsync();
            return Ok(jobs);
        }

        // Helper to get OrganizationId from Claims
        private int GetOrgId()
        {
            var claim = User.Claims.FirstOrDefault(c => 
                c.Type == "OrganizationId" || 
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/organizationid");
            
            if (claim == null)
            {
                var allClaims = string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}"));
                Console.WriteLine($"DEBUG: Org claim missing. Claims: {allClaims}");
                throw new UnauthorizedAccessException($"OrganizationId missing in claims.");
            }
            Console.WriteLine($"DEBUG: Parsed OrgId: {claim.Value}");
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

        // ------------------------
        // 1. Upload CSV
        // ------------------------
        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file, [FromForm] int templateId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var orgId = GetOrgId();

            // Validate template belongs to this organization
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

        // ------------------------
        // 2. Get Job Details
        // ------------------------
        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobLegacy(int jobId) => await GetJob(jobId);

        [HttpGet("job-info/{jobId}")]
        public async Task<IActionResult> GetJob(int jobId)
        {
            Console.WriteLine($"DEBUG: GetJob called for ID: {jobId}");
            var orgId = GetOrgId();
            
            var job = await _context.UploadJobs.FindAsync(jobId);

            if (job == null) 
            {
                Console.WriteLine($"DEBUG: Job {jobId} NOT FOUND in DB.");
                return NotFound($"Job ID {jobId} not found in database.");
            }

            if (job.OrganizationId != orgId)
            {
                Console.WriteLine($"DEBUG: Org mismatch. JobOrg: {job.OrganizationId}, UserOrg: {orgId}");
                return StatusCode(403, $"Forbidden: Job belongs to Org {job.OrganizationId}, but you are Org {orgId}");
            }

            await _context.Entry(job).Reference(j => j.Template).LoadAsync();
            if (job.Template != null)
            {
                await _context.Entry(job.Template).Collection(t => t.Fields).LoadAsync();
            }

            return Ok(job);
        }

        // ------------------------
        // 3. Preview CSV
        // ------------------------
        [HttpGet("{jobId}/preview")]
        public async Task<IActionResult> Preview(int jobId)
        {
            Console.WriteLine($"DEBUG: Preview called for ID: {jobId}");
            var job = await _context.UploadJobs.FindAsync(jobId);
            if (job == null)
            {
                Console.WriteLine($"DEBUG: Preview Job {jobId} NOT FOUND.");
                return NotFound($"Preview failed: Job {jobId} not found.");
            }

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

        // ------------------------
        // 3. Save CSV Mapping
        // ------------------------
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

        // ------------------------
        // 4. Generate PDF from CSV
        // ------------------------
        [HttpPost("{jobId}/generate")]
        public async Task<IActionResult> GeneratePdf(int jobId)
        {
            try
            {
                var job = await _context.UploadJobs
                    .Include(j => j.Template)
                        .ThenInclude(t => t.Fields)
                    .FirstOrDefaultAsync(j => j.Id == jobId);

                if (job == null) return NotFound("Job not found");
                if (job.Template == null) return BadRequest("Template not found for this job");
                if (string.IsNullOrEmpty(job.MappingJson)) return BadRequest("Mapping not saved");

                var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(job.MappingJson);
                if (mapping == null) return BadRequest("Invalid mapping");

                // Read CSV records
                var rawRecords = new List<Dictionary<string, string>>();
                using var reader = new StreamReader(job.CsvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                foreach (var row in csv.GetRecords<dynamic>())
                {
                    // Use case-insensitive dictionary for raw records to handle header casing differences
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in (IDictionary<string, object>)row)
                    {
                        var key = prop.Key?.Trim() ?? ""; // Trim headers
                        dict[key] = prop.Value?.ToString() ?? "";
                    }
                    
                    rawRecords.Add(dict);
                }

                // Translate records based on mapping
                // mapping: { "FieldId": "CSVColumnName" }
                var mappedRecords = new List<Dictionary<string, string>>();
                foreach (var raw in rawRecords)
                {
                    // Use case-insensitive dictionary for mapped records
                    var mappedDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in mapping)
                    {
                        var fieldIdStr = m.Key;
                        var csvColumn = m.Value?.Trim(); // Trim column name from mapping

                        if (!string.IsNullOrEmpty(csvColumn) && raw.TryGetValue(csvColumn, out var value))
                        {
                            // We use FieldID as the key in mappedDict for PDF rendering
                            mappedDict[fieldIdStr] = value;
                        }
                    }
                    mappedRecords.Add(mappedDict);

                    var badge = new Badge
                    {
                        BadgeTemplateId = job.TemplateId,
                        OrganizationId = job.OrganizationId,
                        DataJson = JsonSerializer.Serialize(mappedDict),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = job.CreatedBy
                    };
                    _context.Badges.Add(badge);
                }

                // Generate PDF using translated records
                var filePath = _badgePdfService.GenerateBadgesPdf(job.Template, mappedRecords);

                // Save or Update GeneratedDocument
                var existingDoc = await _context.GeneratedDocuments
                    .FirstOrDefaultAsync(d => d.UploadJobId == job.Id);

                if (existingDoc != null)
                {
                    existingDoc.FilePath = filePath;
                    existingDoc.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    var doc = new GeneratedDocument
                    {
                        UploadJobId = job.Id,
                        FilePath = filePath,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.GeneratedDocuments.Add(doc);
                }

                job.Status = "Completed";
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"SUCCESS: PDF generated and record saved for Job {jobId}");

                return Ok(new { Message = "PDF generated", FilePath = filePath });
            }
            catch (Exception ex)
            {
                Console.WriteLine("------------------------------");
                Console.WriteLine($"FATAL ERROR in GeneratePdf (Job {jobId}):");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
                Console.WriteLine("------------------------------");
                return BadRequest(new { message = $"Generation failed: {ex.Message}" });
            }
        }

        // ------------------------
        // 5. Job History (role-based)
        // ------------------------
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
            {
                query = query.Where(j => j.CreatedBy == userId);
            }

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new
                {
                    j.Id,
                    TemplateName = j.Template.Name,
                    j.Status,
                    CreatedAt = j.CreatedAt, // Return DateTime object for ISO 8601 serialization
                    PdfFile = j.GeneratedDocument != null ? j.GeneratedDocument.FilePath : null,
                    j.ErrorMessage
                })
                .ToListAsync();

            return Ok(jobs);
        }

        // ------------------------
        // 6. Download PDF (AllowAnonymous for browser direct links)
        // ------------------------
        [AllowAnonymous]
        [HttpGet("{jobId}/download")]
        [HttpGet("/api/jobs/{jobId}/download")]
        public async Task<IActionResult> DownloadPdf(int jobId, [FromQuery] bool inline = false)
        {
            var job = await _context.UploadJobs
                .Include(j => j.GeneratedDocument)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return NotFound("Job not found");
            }

            if (job.GeneratedDocument == null)
            {
                return NotFound("PDF not found");
            }

            var filePath = job.GeneratedDocument.FilePath;
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Physical PDF file not found on server.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);

            if (inline)
            {
                // Return inline for previewers
                return File(fileBytes, "application/pdf");
            }

            return File(fileBytes, "application/pdf", fileName);
        }
    }
}

//dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL