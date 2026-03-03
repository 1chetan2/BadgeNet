using BadgeCraft_Net.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadgeCraft_Net.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var orgId = GetOrgId();
            var userId = GetUserId();
            var isAdmin = IsAdmin();

            var totalTemplates = await _context.BadgeTemplates
                .CountAsync(t => t.OrganizationId == orgId);

            var jobsQuery = _context.UploadJobs
                .Where(j => j.OrganizationId == orgId);

            if (!isAdmin)
            {
                jobsQuery = jobsQuery.Where(j => j.CreatedBy == userId);
            }

            var totalJobs = await jobsQuery.CountAsync();

            var completedJobs = await jobsQuery
                .CountAsync(j => j.Status == "Completed" || j.Status == "PdfGenerated");

            var failedJobs = await jobsQuery
                .CountAsync(j => j.Status == "Failed");

            var totalUsers = 0;
            if (isAdmin)
            {
                totalUsers = await _context.Users
                    .CountAsync(u => u.OrganizationId == orgId);
            }

            return Ok(new
            {
                totalTemplates,
                totalJobs,
                completedJobs,
                failedJobs,
                totalUsers
            });
        }

        private int GetOrgId()
        {
            var claim = User.Claims.FirstOrDefault(c => 
                c.Type == "OrganizationId" || 
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/organizationid");
            
            if (claim == null)
            {
                throw new UnauthorizedAccessException("OrganizationId missing in claims.");
            }
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
    }
}
