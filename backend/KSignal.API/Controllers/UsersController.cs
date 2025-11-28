using KSignal.API.Data;
using KSignal.API.Models;
using KSignals.DTO;
using Microsoft.AspNetCore.Authorization;
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

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfile(CancellationToken cancellationToken)
    {
        var firebaseId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(firebaseId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.FirebaseId == firebaseId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var response = new UserProfileResponse
        {
            Username = user.Username ?? string.Empty,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return Ok(response);
    }

    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var firebaseId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(firebaseId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) && string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error = "Please provide at least a first or last name" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == firebaseId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var token = CreateJwt(user);
        var response = new SignInResponse
        {
            Token = token,
            Username = user.Username ?? user.Email ?? user.FirebaseId,
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty
        };

        return Ok(response);
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

    [HttpPost("setUsername")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetUsername([FromBody] SetUsernameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirebaseId))
        {
            return BadRequest(new { error = "firebaseId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { error = "username is required" });
        }

        // Validate username format (alphanumeric and underscores only, 3-30 chars)
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]{3,30}$"))
        {
            return BadRequest(new { error = "Username must be 3-30 characters and contain only letters, numbers, and underscores" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);

        // Check if username is already taken by another user
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);
        if (existingUser != null && existingUser.FirebaseId != request.FirebaseId)
        {
            return BadRequest(new { error = "Username is already taken" });
        }

        // Find user by FirebaseId
        var user = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == request.FirebaseId, cancellationToken);
        if (user == null)
        {
            return BadRequest(new { error = "User not found" });
        }

        user.Username = request.Username;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Generate new JWT with updated username
        var token = CreateJwt(user);
        var response = new SignInResponse
        {
            Token = token,
            Username = user.Username,
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty
        };

        return Ok(response);
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
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty
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
