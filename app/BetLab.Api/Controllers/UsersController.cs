using BetLab.Application.DTOs;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly BetLabDbContext _context;

        public UsersController(BetLabDbContext context)
        {
            _context = context;
        }

        [HttpGet("demo")]
        public async Task<ActionResult<DemoUserDto>> GetDemoUser()
        {
            var user = await _context.Users
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Email == "demo@betlab.local");

            if (user == null)
                return NotFound();

            var result = new DemoUserDto
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                IsActive = user.IsActive,
                Balance = user.Wallet?.Balance ?? 0,
                Currency = user.Wallet?.Currency ?? "RON"
            };

            return Ok(result);
        }
    }
}