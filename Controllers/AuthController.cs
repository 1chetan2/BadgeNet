using BadgeCraft_Net.Data;
using BadgeCraft_Net.DTOs;
using BadgeCraft_Net.Models;
using BadgeCraft_Net.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BadgeCraft_Net.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _jwt;

        public AuthController(AppDbContext context, TokenService jwt)
        {
            _context = context;
            _jwt = jwt;     
        }       
                                                              
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {                                                           
            if (_context.Users.Any(x => x.Email == dto.Email))
                return BadRequest(new { message = "Email already exists" });
                                                                                                                                           
            var org = new Organization { Name = dto.OrganizationName };     
            _context.Organizations.Add(org);                                    
            await _context.SaveChangesAsync();      
                                                                                                  
            var user = new User
            {                                                                 
                Name = dto.Name,
                Email = dto.Email,                               
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),                                      
                OrganizationId = org.Id,
                Role = "OrgAdmin"           
            };
                                                                                            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Registered successfully" });
        }

        [HttpPost("login")]
        public IActionResult Login(LoginDto dto)
        {
            var user = _context.Users.FirstOrDefault(x => x.Email == dto.Email);        
                
            if (user == null)                                                                   
                return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))     
                return Unauthorized();

            if (!user.IsGranted)
                return Unauthorized(new { message = "Your account access has not allowed" });

            var token = _jwt.GenerateToken(user);   

            return Ok(new { token });   
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            var header = Request.Headers["Authorization"].ToString();
            return Ok(new { header });
        }



    }
}
