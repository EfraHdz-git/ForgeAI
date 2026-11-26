namespace ForgeAI.Services.Auth;

public static class PasswordService
{
    public static string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public static bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    public static (bool valid, string? error) ValidateStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password is required.");
        if (password.Length < 8)
            return (false, "Password must be at least 8 characters.");
        if (password.Length > 128)
            return (false, "Password is too long.");
        if (!password.Any(char.IsUpper))
            return (false, "Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsDigit))
            return (false, "Password must contain at least one number.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return (false, "Password must contain at least one special character (!@#$%&*).");
        return (true, null);
    }
}
