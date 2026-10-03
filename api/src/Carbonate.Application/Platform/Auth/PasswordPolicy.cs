namespace Carbonate.Application.Platform.Auth;

/// <summary>Password rules for new and changed passwords: 12 characters minimum and not in a known breach.</summary>
public sealed class PasswordPolicy(IPwnedPasswordChecker pwned)
{
    public const int MinimumLength = 12;

    /// <summary>Returns the reasons the password is not acceptable; empty means it is fine.</summary>
    public async Task<IReadOnlyList<string>> ValidateAsync(string password, CancellationToken ct)
    {
        var problems = new List<string>();

        if (password.Length < MinimumLength)
        {
            problems.Add($"Use at least {MinimumLength} characters.");
        }

        // Skip the network call when the password is already rejected.
        if (problems.Count == 0 && await pwned.IsPwnedAsync(password, ct))
        {
            problems.Add("That password has appeared in a data breach. Choose a different one.");
        }

        return problems;
    }
}
