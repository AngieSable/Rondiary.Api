using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rondiary.Api.Models;

namespace Rondiary.Api.Controllers;

public record AuthRequest(string? Email, string? Password);
public record AuthResponse(string Token, Guid UserId, string Email);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration cfg) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(AuthRequest r)
    {
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        var password = r.Password ?? "";

        if (email.Length > 100 || !MailAddress.TryCreate(email, out _) || !email.Contains('@'))
            return BadRequest(new { message = "Введите корректную почту" });
        if (password.Length < 6 || password.Length > 64)
            return BadRequest(new { message = "Пароль от 6 до 64 символов" });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Эта почта уже зарегистрирована" });

        var user = new Rondiary.Api.Models.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new AuthResponse(MakeToken(user), user.Id, user.Email);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(AuthRequest r)
    {
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        bool ok = false;
        try { ok = user != null && BCrypt.Net.BCrypt.Verify(r.Password ?? "", user.PasswordHash); }
        catch {  }

        if (!ok) return BadRequest(new { message = "Неверная почта или пароль" });
        return new AuthResponse(MakeToken(user!), user!.Id, user.Email);
    }

    private string MakeToken(Rondiary.Api.Models.User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            },
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}