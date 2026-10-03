using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Microsoft.AspNetCore.Identity;

namespace Carbonate.Infrastructure.Platform.Auth;

/// <summary>PBKDF2 hashing through the framework hasher, which salts and versions the hash for us.</summary>
internal sealed class PasswordService : IPasswordService
{
    private static readonly AppUser DummyUser = new();
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly string _dummyHash;

    public PasswordService()
    {
        _dummyHash = _hasher.HashPassword(DummyUser, Guid.NewGuid().ToString("N"));
    }

    public string Hash(AppUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(AppUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public void VerifyDummy(string password) => _hasher.VerifyHashedPassword(DummyUser, _dummyHash, password);
}
