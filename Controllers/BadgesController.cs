using BadgeCraft_Net.Data;
using BadgeCraft_Net.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadgeCraft_Net.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BadgesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BadgesController(AppDbContext context)
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

        // Get Template for Editor Preview
        [HttpGet("template/{templateId}")]
        public async Task<IActionResult> GetTemplate(int templateId)
        {
            var orgId = GetOrgId();

            var template = await _context.BadgeTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.OrganizationId == orgId);

            if (template == null)
                return NotFound();

            return Ok(template);
        }
    }
}