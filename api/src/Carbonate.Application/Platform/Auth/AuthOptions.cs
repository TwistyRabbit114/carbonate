namespace Carbonate.Application.Platform.Auth;

public class AuthOptions
{
    public const string Section = "Auth";

    public int AccessTokenMinutes { get; set; } = 15;
    public int SessionHours { get; set; } = 8;
    public int LockoutThreshold { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int MfaTokenMinutes { get; set; } = 5;

    /// <summary>Base64 of a 32-byte key that encrypts TOTP secrets at rest. Comes from Key Vault in Azure.</summary>
    public string MfaEncryptionKey { get; set; } = "";
}

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
}
