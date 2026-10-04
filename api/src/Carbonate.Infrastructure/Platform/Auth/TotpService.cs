using Carbonate.Application.Platform.Auth;
using OtpNet;

namespace Carbonate.Infrastructure.Platform.Auth;

internal sealed class TotpService : ITotpService
{
    private const string Issuer = "Carbonate";

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildUri(string secret, string accountName) =>
        $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(accountName)}?secret={secret}&issuer={Issuer}&digits=6&period=30";

    public bool Verify(string secret, string code)
    {
        var trimmed = code.Trim();
        if (trimmed.Length != 6 || !trimmed.All(char.IsAsciiDigit))
        {
            return false;
        }

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(trimmed, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }
}
