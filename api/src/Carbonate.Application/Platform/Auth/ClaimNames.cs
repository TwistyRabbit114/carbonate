namespace Carbonate.Application.Platform.Auth;

/// <summary>Claim types used in the access token and the short-lived MFA token.</summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Permission = "perm";

    /// <summary>Set on the MFA token only. It marks a token that must not be accepted as a sign-in.</summary>
    public const string Purpose = "purpose";

    public const string MfaPurpose = "mfa";
}
