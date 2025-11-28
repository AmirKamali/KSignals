using KSignal.API.Data;
using KSignal.API.Models;
using KSignals.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly KalshiDbContext _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(KalshiDbContext db, ILogger<UsersController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirebaseId))
        {
            return BadRequest(new { error = "firebaseId is required" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == request.FirebaseId, cancellationToken);
        var now = DateTime.UtcNow;

        if (existing == null)
        {
            var user = new User
            {
                FirebaseId = request.FirebaseId,
                Username = request.Username,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                IsComnEmailOn = request.IsComnEmailOn,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Users.Add(user);
        }
        else
        {
            existing.Username = request.Username ?? existing.Username;
            existing.FirstName = request.FirstName ?? existing.FirstName;
            existing.LastName = request.LastName ?? existing.LastName;
            existing.Email = request.Email ?? existing.Email;
            existing.IsComnEmailOn = request.IsComnEmailOn;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirebaseId))
        {
            return BadRequest(new { error = "firebaseId is required" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == request.FirebaseId, cancellationToken);
        var now = DateTime.UtcNow;

        if (user == null)
        {
            user = new User
            {
                FirebaseId = request.FirebaseId,
                Username = request.Username,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                IsComnEmailOn = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Username = request.Username ?? user.Username;
            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.Email = request.Email ?? user.Email;
            user.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var token = CreateJwt(user);
        var response = new SignInResponse
        {
            Token = token,
            Username = user.Username ?? user.Email ?? user.FirebaseId,
            Name = $"{user.FirstName} {user.LastName}".Trim()
        };

        return Ok(response);
    }

    private string CreateJwt(User user)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT_SECRET environment variable is required to issue tokens.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.FirebaseId),
            new Claim("username", user.Username ?? user.Email ?? user.FirebaseId),
            new Claim("name", $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("email", user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: "ksignals",
            audience: "ksignals",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
