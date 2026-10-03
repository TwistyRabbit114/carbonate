using System.Security.Cryptography;
using System.Text;
using Carbonate.Application.Platform.Auth;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Platform.Auth;

/// <summary>
/// Checks a password against the Pwned Passwords range API. Only the first five characters of the
/// SHA-1 hash leave the server (k-anonymity), never the password.
/// </summary>
internal sealed partial class PwnedPasswordChecker(HttpClient http, ILogger<PwnedPasswordChecker> logger)
    : IPwnedPasswordChecker
{
    public async Task<bool> IsPwnedAsync(string password, CancellationToken ct)
    {
#pragma warning disable CA5350 // SHA-1 is what the range API is keyed on; it is not used for security here.
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350
        var prefix = hash[..5];
        var suffix = hash[5..];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            request.Headers.Add("Add-Padding", "true");
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(':');
                if (parts.Length == 2 && parts[0] == suffix && parts[1] != "0")
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Fail open: a breach-check outage must not stop people setting a password.
            LogUnavailable(ex.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pwned Passwords check unavailable ({Reason}); allowing the password")]
    private partial void LogUnavailable(string reason);
}
