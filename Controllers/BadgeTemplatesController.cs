using BadgeCraft_Net.Data;
using BadgeCraft_Net.DTOs;
using BadgeCraft_Net.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
            var orgClaim = User.Claims
                .FirstOrDefault(c => c.Type == "OrganizationId");

            if (orgClaim == null)
                throw new UnauthorizedAccessException("OrganizationId claim missing");
            return int.Parse(orgClaim.Value);
        }


        //  GET ALL (Admin + User)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orgId = GetOrgId();

            var templates = await _context.BadgeTemplates
                .Where(t => t.OrganizationId == orgId)
                .ToListAsync();

            return Ok(templates);
        }

        //  CREATE (Admin Only)
        [Authorize(Roles = "OrgAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create(CreateBadgeTemplateDto dto)
        {
            var orgId = GetOrgId();

            var template = new BadgeTemplate
            {
                Title = dto.Title,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                OrganizationId = orgId
            };

            _context.BadgeTemplates.Add(template);
            await _context.SaveChangesAsync();
            return Ok(template);
        }

        // UPDATE (Admin Only)
        [Authorize(Roles = "OrgAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateBadgeTemplateDto dto)
        {
            var orgId = GetOrgId();
            var template = await _context.BadgeTemplates
                .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            template.Title = dto.Title;
            template.Description = dto.Description;
            template.ImageUrl = dto.ImageUrl;

            await _context.SaveChangesAsync();
            return Ok(template);
        }

        //  DELETE (Admin Only)
        [Authorize(Roles = "OrgAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            _context.BadgeTemplates.Remove(template);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}