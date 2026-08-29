using System.Security.Claims;
using BetLab.Api.Services;
using BetLab.Application.DTOs;
using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly BetLabDbContext _context;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(
            UserManager<AppUser> userManager,
            BetLabDbContext context,
            JwtTokenService jwtTokenService)
        {
            _userManager = userManager;
            _context = context;
            _jwtTokenService = jwtTokenService;
        }

    
        private static bool IsValidCnp(string cnp)
        {
            return cnp.Length == 13 && cnp.All(char.IsDigit);
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Email is required.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Password is required.");

            // Validare CNP 
            if (string.IsNullOrWhiteSpace(request.Cnp))
                return BadRequest("CNP-ul este obligatoriu pentru crearea unui cont.");

            var cnp = request.Cnp.Trim();
            if (!IsValidCnp(cnp))
                return BadRequest("CNP invalid. Verifică dacă ai introdus corect toate cele 13 cifre.");

            // Verificare Registru de Autoexcludere (simulare ONJN) 
            var isCnpExcluded = await _context.CnpExclusions.AnyAsync(e => e.Cnp == cnp);
            if (isCnpExcluded)
                return BadRequest("CNP_EXCLUDED");

            // Verificare email unic
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest("A user with this email already exists.");

            var user = new AppUser
            {
                Id             = Guid.NewGuid(),
                Email          = request.Email,
                UserName       = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName,
                EmailConfirmed = true,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow,
                Cnp            = cnp
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
                return BadRequest(createResult.Errors.Select(e => e.Description));

            var wallet = new Wallet
            {
                UserId   = user.Id,
                Balance  = 100m,
                Currency = "RON"
            };

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();

            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId      = wallet.Id,
                Type          = "InitialCredit",
                Amount        = 100m,
                BalanceBefore = 0m,
                BalanceAfter  = 100m,
                Description   = "Initial demo credit on registration"
            });

            await _context.SaveChangesAsync();

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(user, roles);

            return Ok(new AuthResponseDto
            {
                Token        = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId       = user.Id,
                Email        = user.Email    ?? string.Empty,
                UserName     = user.UserName ?? string.Empty,
                Roles        = roles.ToList()
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Email is required.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Password is required.");

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized("Invalid email or password.");

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
                return Unauthorized("Invalid email or password.");

            if (!user.IsActive)
                return Unauthorized("User is inactive.");

            var roles = await _userManager.GetRolesAsync(user);
            var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(user, roles);

            return Ok(new AuthResponseDto
            {
                Token        = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId       = user.Id,
                Email        = user.Email    ?? string.Empty,
                UserName     = user.UserName ?? string.Empty,
                Roles        = roles.ToList()
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserDto>> Me()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized();

            var userId = Guid.Parse(userIdClaim);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return NotFound();

            return Ok(new CurrentUserDto
            {
                UserId   = user.Id,
                Email    = user.Email    ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                IsActive = user.IsActive
            });
        }
    }
}
