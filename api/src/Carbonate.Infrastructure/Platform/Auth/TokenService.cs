using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Carbonate.Infrastructure.Platform.Auth;

/// <summary>The one place token validation settings live, so the API and this service never disagree.</summary>
public static class TokenValidation
{
    public static SymmetricSecurityKey SigningKey(JwtOptions jwt)
    {
        var bytes = Encoding.UTF8.GetBytes(jwt.SigningKey);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        return new SymmetricSecurityKey(bytes);
    }

    public static TokenValidationParameters Parameters(JwtOptions jwt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(jwt),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimNames.Name,
        RoleClaimType = ClaimNames.Role,
    };
}

internal sealed class TokenService(IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions, TimeProvider clock)
    : ITokenService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(AppUser user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(_auth.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.UserId.ToString()),
            new(ClaimNames.Name, user.FullName),
            new(ClaimNames.Email, user.Email),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(ClaimNames.Permission, p)));

        return new AccessToken(Create(claims, expires), expires);
    }

    public string CreateMfaToken(Guid userId)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(_auth.MfaTokenMinutes);
        List<Claim> claims =
        [
            new(ClaimNames.Subject, userId.ToString()),
            new(ClaimNames.Purpose, ClaimNames.MfaPurpose),
        ];

        return Create(claims, expires);
    }

    public async Task<Guid?> ValidateMfaTokenAsync(string token)
    {
        var parameters = TokenValidation.Parameters(_jwt);
        parameters.LifetimeValidator = (_, expires, _, _) => expires is null || expires > clock.GetUtcNow().UtcDateTime;

        var result = await _handler.ValidateTokenAsync(token, parameters);
        if (!result.IsValid)
        {
            return null;
        }

        var purpose = result.ClaimsIdentity.FindFirst(ClaimNames.Purpose)?.Value;
        var subject = result.ClaimsIdentity.FindFirst(ClaimNames.Subject)?.Value;

        return purpose == ClaimNames.MfaPurpose && Guid.TryParse(subject, out var userId) ? userId : null;
    }

    public (string Token, string Hash) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, HashRefreshToken(token));
    }

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string Create(IEnumerable<Claim> claims, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = clock.GetUtcNow().UtcDateTime,
            Expires = expires,
            SigningCredentials = new SigningCredentials(TokenValidation.SigningKey(_jwt), SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }
}
