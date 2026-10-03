using System.Security.Cryptography;
using System.Text;
using Carbonate.Application.Platform.Auth;
using Microsoft.Extensions.Options;

namespace Carbonate.Infrastructure.Platform.Auth;

/// <summary>Encrypts small secrets (TOTP seeds) with AES-256-GCM. Output is base64 of nonce, tag, ciphertext.</summary>
internal sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public SecretProtector(IOptions<AuthOptions> options)
    {
        try
        {
            _key = Convert.FromBase64String(options.Value.MfaEncryptionKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Auth:MfaEncryptionKey must be base64.", ex);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Auth:MfaEncryptionKey must decode to 32 bytes.");
        }
    }

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string protectedText)
    {
        var data = Convert.FromBase64String(protectedText);
        var nonce = data[..NonceSize];
        var tag = data[NonceSize..(NonceSize + TagSize)];
        var cipher = data[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
