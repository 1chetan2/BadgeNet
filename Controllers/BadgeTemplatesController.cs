using BadgeCraft_Net.Data;
using BadgeCraft_Net.DTOs;
using BadgeCraft_Net.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadgeCraft_Net.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BadgeTemplatesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BadgeTemplatesController(AppDbContext context)
        {
            _context = context;
        }

        private int GetOrgId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "OrganizationId");
            if (claim == null)
                throw new UnauthorizedAccessException("OrganizationId missing");

            return int.Parse(claim.Value);
        }

        // =========================
        // GET ALL Templates
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orgId = GetOrgId();

            var templates = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .Where(t => t.OrganizationId == orgId)
                .ToListAsync();

            return Ok(templates);
        }

        // =========================
        // GET SINGLE Template
        // =========================
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            return Ok(template);
        }

        // =========================
        // CREATE Template + Fields
        // =========================
        [Authorize(Roles = "OrgAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create(CreateBadgeTemplateDto dto)
        {
            var orgId = GetOrgId();

            var template = new BadgeTemplate
            {
                Name = dto.Name,
                Status = dto.Status,
                PageSize = dto.PageSize,
                BadgesPerPage = dto.BadgesPerPage,
                BadgeWidth = dto.BadgeWidth,
                BadgeHeight = dto.BadgeHeight,
                Background = dto.Background,
                OrganizationId = orgId
            };

            _context.BadgeTemplates.Add(template);
            await _context.SaveChangesAsync();

            // Save Fields
            if (dto.Fields != null && dto.Fields.Any())
            {
                var fields = dto.Fields.Select(f => new BadgeTemplateField
                {
                    BadgeTemplateId = template.Id,
                    Type = f.Type,
                    Key = f.Key,
                    X = f.X,
                    Y = f.Y,
                    Width = f.Width,
                    Height = f.Height,
                    StyleJson = f.StyleJson,
                    IsRequired = f.IsRequired,
                    DefaultValue = f.DefaultValue
                }).ToList();

                _context.BadgeTemplateFields.AddRange(fields);
                await _context.SaveChangesAsync();
            }

            // Return with fields
            var createdTemplate = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstAsync(t => t.Id == template.Id);

            return Ok(createdTemplate);
        }

        // =========================
        // UPDATE Template + Fields
        // =========================
        [Authorize(Roles = "OrgAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateBadgeTemplateDto dto)
        {
            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            // Update Template
            template.Name = dto.Name;
            template.Status = dto.Status;
            template.PageSize = dto.PageSize;
            template.BadgesPerPage = dto.BadgesPerPage;
            template.BadgeWidth = dto.BadgeWidth;
            template.BadgeHeight = dto.BadgeHeight;
            template.Background = dto.Background;

            // Remove old fields
            _context.BadgeTemplateFields.RemoveRange(template.Fields);

            // Add new fields
            if (dto.Fields != null && dto.Fields.Any())
            {
                var newFields = dto.Fields.Select(f => new BadgeTemplateField
                {
                    BadgeTemplateId = template.Id,
                    Type = f.Type,
                    Key = f.Key,
                    X = f.X,
                    Y = f.Y,
                    Width = f.Width,
                    Height = f.Height,
                    StyleJson = f.StyleJson,
                    IsRequired = f.IsRequired,
                    DefaultValue = f.DefaultValue
                }).ToList();

                _context.BadgeTemplateFields.AddRange(newFields);
            }

            await _context.SaveChangesAsync();

            var updatedTemplate = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstAsync(t => t.Id == id);

            return Ok(updatedTemplate);
        }

        // =========================
        // DELETE Template
        // =========================
        [Authorize(Roles = "OrgAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            _context.BadgeTemplateFields.RemoveRange(template.Fields);
            _context.BadgeTemplates.Remove(template);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Template deleted successfully" });
        }
    }
}