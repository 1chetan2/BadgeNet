//using BadgeCraft_Net.Data;
//using BadgeCraft_Net.DTOs;
//using BadgeCraft_Net.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;


//namespace BadgeCraft_Net.Controllers
//{
//[Authorize]
//[Route("api/[controller]")]
//[ApiController]
//public class UsersController : ControllerBase

//{
//    private readonly AppDbContext _context;

//        public UsersController(AppDbContext context)
//        {
//            _context = context;
//        }

//        private int GetOrgId()
//        {
//            var orgClaim = User.Claims
//                .FirstOrDefault(c => c.Type == "OrganizationId");

//            if (orgClaim == null)
//                throw new UnauthorizedAccessException("OrganizationId claim missing");

//            return int.Parse(orgClaim.Value);
//        }



//    // GET ALL USERS (same organization)
//    [Authorize(Roles = "OrgAdmin")]
//    [HttpGet]
//        public async Task<IActionResult> GetUsers()
//        {
//            var orgId = GetOrgId();

//            var users = await _context.Users
//                .Where(u => u.OrganizationId == orgId)
//                .Select(u => new
//                {
//                    u.Id,
//                    u.Email,
//                    u.Role
//                })
//                .ToListAsync();

//            return Ok(users);
//        }

//    // CREATE USER
//    [Authorize(Roles = "OrgAdmin")]
//    [HttpPost]
//        public async Task<IActionResult> Create(CreateUserDto dto)
//        {
//            var orgId = GetOrgId();

//            var user = new User
//            {
//                Email = dto.Email,
//                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
//                Role = dto.Role,
//                OrganizationId = orgId
//            };

//            _context.Users.Add(user);
//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                user.Id,
//                user.Email,
//                user.Role
//            });
//        }

//        // UPDATE USER
//        [HttpPut("{id}")]
//        public async Task<IActionResult> Update(int id, UpdateUserDto dto)
//        {
//            var orgId = GetOrgId();

//            var user = await _context.Users
//                .FirstOrDefaultAsync(u =>
//                    u.Id == id &&
//                    u.OrganizationId == orgId);

//            if (user == null)
//                return NotFound();

//            user.Email = dto.Email;
//            user.Role = dto.Role;

//            await _context.SaveChangesAsync();

//            return Ok();
//        }

//    // DELETE USER
//    [Authorize(Roles = "OrgAdmin")]
//    [HttpDelete("{id}")]
//        public async Task<IActionResult> Delete(int id)
//        {
//            var orgId = GetOrgId();

//            var user = await _context.Users
//                .FirstOrDefaultAsync(u =>
//                    u.Id == id &&
//                    u.OrganizationId == orgId);

//            if (user == null)
//                return NotFound();

//            _context.Users.Remove(user);
//            await _context.SaveChangesAsync();

//            return Ok();
//        }

//        [Authorize]
//        [HttpGet("claims")]
//        public IActionResult ClaimsDebug()
//        {
//            return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
//        }
//      }
//    }








using BadgeCraft_Net.Data;
using BadgeCraft_Net.DTOs;
using BadgeCraft_Net.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
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

    private string GetUserEmail()
    {
        return User.Claims
            .FirstOrDefault(c => c.Type == "email")?.Value;
    }

    //  GET USERS
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var orgId = GetOrgId();
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var email = GetUserEmail();

        if (role == "OrgAdmin")
        {
            var users = await _context.Users
                .Where(u => u.OrganizationId == orgId)
                .Select(u => new { id = u.Id, name = u.Name, email = u.Email, role = u.Role, isGranted = u.IsGranted, createdAt = u.CreatedAt })
                .ToListAsync();

            return Ok(users);
        }

        //  OrgUser → Only Self
        var selfUser = await _context.Users
            .Where(u => u.OrganizationId == orgId && u.Email == email)
            .Select(u => new { id = u.Id, email = u.Email, role = u.Role, isGranted = u.IsGranted, createdAt = u.CreatedAt })
            .ToListAsync();

        return Ok(selfUser);
    }

    // CREATE USER (Admin Only)
    [Authorize(Roles = "OrgAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        var orgId = GetOrgId();

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            OrganizationId = orgId,
            IsGranted = dto.IsGranted
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { id = user.Id, name = user.Name, email = user.Email, role = user.Role, isGranted = user.IsGranted, createdAt = user.CreatedAt });
    }

    // GET USER BY ID
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var orgId = GetOrgId();

        var user = await _context.Users
            .Where(u => u.Id == id && u.OrganizationId == orgId)
            .Select(u => new
            {
                id = u.Id,
                name = u.Name,
                email = u.Email,
                role = u.Role,
                isGranted = u.IsGranted,
                createdAt = u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    // UPDATE USER (Admin Only)
    [Authorize(Roles = "OrgAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateUserDto dto)
    {
        var orgId = GetOrgId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                u.OrganizationId == orgId);

        if (user == null)
            return NotFound();

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.Role = dto.Role;
        user.IsGranted = dto.IsGranted;

        await _context.SaveChangesAsync();

        return Ok();
    }

    // DELETE USER (Admin Only)
    [Authorize(Roles = "OrgAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var orgId = GetOrgId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                u.OrganizationId == orgId);

        if (user == null)
            return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok();
    }

    // Debug Claims
    [HttpGet("claims")]
    public IActionResult ClaimsDebug()
    {
        return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
    }
}









