namespace Carbonate.Application.Common;

/// <summary>A save lost to someone else's change. The service turns it into a 409 with the current state.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was changed by someone else.")
    {
    }
}

public static class RowVersions
{
    /// <summary>Reads the base64 row version a client sent back. A missing or malformed one is a 400.</summary>
    public static byte[] Decode(string? rowVersion)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            try
            {
                var bytes = Convert.FromBase64String(rowVersion);
                if (bytes.Length > 0)
                {
                    return bytes;
                }
            }
            catch (FormatException)
            {
                // Falls through to the validation error below.
            }
        }

        throw ProblemException.Validation(new Dictionary<string, string[]>
        {
            ["rowVersion"] = ["Send back the rowVersion you were given."],
        });
    }

    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);
}

/// <summary>Runs work as one database transaction, so a half-built record never reaches the database.</summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
