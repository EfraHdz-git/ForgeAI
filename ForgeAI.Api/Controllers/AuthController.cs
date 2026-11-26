using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ForgeAI.Api.Middleware;
using ForgeAI.Core.Interfaces;
using ForgeAI.Services.Auth;
using ForgeAI.Services.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly JwtService _jwt;
    private readonly MagicLinkService _magicLink;
    private readonly EmailService _email;
    private readonly string _baseUrl;

    public AuthController(
        IUserRepository users, JwtService jwt,
        MagicLinkService magicLink, EmailService email,
        IConfiguration config)
    {
        _users = users;
        _jwt = jwt;
        _magicLink = magicLink;
        _email = email;
        _baseUrl = config["App:BaseUrl"] ?? "https://forgeai.codetrend.com";
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        // Validate email format
        if (!IsValidEmail(request.Email))
            return BadRequest(new { Error = "Enter a valid email address." });

        // Validate password strength
        var (valid, error) = PasswordService.ValidateStrength(request.Password);
        if (!valid) return BadRequest(new { Error = error });

        // Check for existing account
        var existing = await _users.GetByEmailAsync(request.Email);
        if (existing != null)
        {
            if (!existing.EmailVerified)
                return Conflict(new { Error = "Account exists but email not verified. Check your inbox or sign up with a different email." });
            return Conflict(new { Error = "An account with this email already exists." });
        }

        // Generate verification token
        var verifyToken = GenerateToken();

        var user = new Core.Models.User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordService.Hash(request.Password),
            EmailVerified = false,
            EmailVerifyToken = verifyToken,
            Tier = "free"
        };

        await _users.CreateAsync(user);

        // Send verification email
        var verifyUrl = $"{_baseUrl}/?verify={verifyToken}";
        await _email.SendVerificationAsync(user.Email, verifyUrl);

        return Ok(new { Message = "Account created. Please check your email to verify.", NeedsVerification = true });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { Error = "Token is required." });

        var user = await _users.GetByVerifyTokenAsync(request.Token);
        if (user == null)
            return BadRequest(new { Error = "Invalid or expired verification link." });

        if (user.EmailVerified)
            return BadRequest(new { Error = "Email already verified." });

        await _users.MarkEmailVerifiedAsync(user.Id);
        await _users.UpdateLastLoginAsync(user.Id);

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.Tier, user.IsAdmin);
        return Ok(new { Token = jwt, User = UserDto(user) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { Error = "Email and password are required." });

        var user = await _users.GetByEmailAsync(request.Email);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized(new { Error = "Invalid email or password." });

        if (!PasswordService.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { Error = "Invalid email or password." });

        if (!user.EmailVerified)
            return StatusCode(403, new { Error = "Please verify your email before signing in.", NeedsVerification = true });

        await _users.UpdateLastLoginAsync(user.Id);

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.Tier, user.IsAdmin);
        return Ok(new { Token = jwt, User = UserDto(user) });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { Error = "Email is required." });

        var user = await _users.GetByEmailAsync(request.Email);
        if (user != null && user.EmailVerified)
            await _magicLink.SendPasswordResetAsync(request.Email);

        return Ok(new { Message = "If an account exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { Error = "Token is required." });

        var (valid, error) = PasswordService.ValidateStrength(request.Password);
        if (!valid) return BadRequest(new { Error = error });

        var user = await _magicLink.VerifyTokenAsync(request.Token);
        if (user == null)
            return Unauthorized(new { Error = "Invalid or expired reset link." });

        await _users.UpdatePasswordAsync(user.Id, PasswordService.Hash(request.Password));

        var jwt = _jwt.GenerateToken(user.Id, user.Email, user.Tier, user.IsAdmin);
        return Ok(new { Token = jwt, User = UserDto(user) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = HttpContext.GetUserId();
        if (userId == null)
            return Unauthorized(new { Error = "Not authenticated." });

        var user = await _users.GetByIdAsync(userId.Value);
        if (user == null) return Unauthorized(new { Error = "User not found." });

        return Ok(UserDto(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest(new { Error = "Current password is required." });

        var (valid, error) = PasswordService.ValidateStrength(request.NewPassword);
        if (!valid) return BadRequest(new { Error = error });

        var user = await _users.GetByIdAsync(userId.Value);
        if (user == null) return Unauthorized(new { Error = "User not found." });

        if (!PasswordService.Verify(request.CurrentPassword, user.PasswordHash!))
            return BadRequest(new { Error = "Current password is incorrect." });

        await _users.UpdatePasswordAsync(user.Id, PasswordService.Hash(request.NewPassword));
        return Ok(new { Message = "Password updated." });
    }

    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { Message = "Logged out." });

    private static object UserDto(Core.Models.User u) => new
    {
        u.Id, u.Email, u.Name, u.Tier,
        u.ReportsUsed, u.PrototypesUsed, u.EmailVerified, u.IsAdmin
    };

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public class SignupRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class VerifyEmailRequest
{
    public string Token { get; set; } = "";
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
