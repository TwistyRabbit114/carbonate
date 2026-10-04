using Carbonate.Application.Common;

namespace Carbonate.Application.Platform.Files;

/// <summary>
/// What Carbonate will accept as an upload (FR-31). Kept away from the storage client so the rules can
/// be tested on their own and so there is one place to look when someone asks "why was my file refused".
/// </summary>
public static class FileValidation
{
    /// <summary>
    /// Photos from a phone, plus PDFs for health and safety files and purchase orders. Deliberately
    /// short: every type added here is a type the browser might be persuaded to render.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["image/heic"] = [".heic"],
        ["image/heif"] = [".heic", ".heif"],
        ["application/pdf"] = [".pdf"],
    };

    public static IReadOnlyCollection<string> AllowedContentTypes => AllowedTypes.Keys;

    /// <summary>
    /// Checks the declared type and the extension agree and are both on the allow-list.
    /// </summary>
    /// <remarks>
    /// Both, not either: a <c>.pdf</c> declared as <c>image/png</c> is either a mistake or an attempt,
    /// and neither should be stored. This does not sniff magic bytes — the defence that actually
    /// matters is that nothing is ever served inline (see <see cref="IFileStorage"/>).
    /// </remarks>
    /// <exception cref="ProblemException">400 with the field name the form uses.</exception>
    public static string ValidateType(string fileName, string contentType, string field = "file")
    {
        var type = Normalise(contentType);

        if (!AllowedTypes.TryGetValue(type, out var extensions))
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                [field] = [$"Files of type '{contentType}' are not accepted. Allowed: JPEG, PNG, WebP, HEIC, PDF."],
            });
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                [field] = ["The file extension does not match the file type."],
            });
        }

        return type;
    }

    /// <exception cref="ProblemException">400 when the file is empty or over the cap.</exception>
    public static void ValidateSize(long sizeBytes, long maxBytes, string field = "file")
    {
        if (sizeBytes <= 0)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                [field] = ["The file is empty."],
            });
        }

        if (sizeBytes > maxBytes)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                [field] = [$"The file is larger than the {maxBytes / (1024 * 1024)} MB limit."],
            });
        }
    }

    /// <summary>
    /// A safe name to hand back to a browser in <c>Content-Disposition</c>: no path, no control
    /// characters, no quotes, and never empty.
    /// </summary>
    public static string SafeDownloadName(string fileName)
    {
        // Split on both separators by hand rather than using Path.GetFileName: the API runs on Linux,
        // where a backslash is an ordinary character, and a Windows client will still send one.
        var name = (fileName ?? "").Split('/', '\\')[^1];
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();

        if (cleaned.Length == 0)
        {
            return "download";
        }

        return cleaned.Length > 180 ? cleaned[^180..] : cleaned;
    }

    /// <summary>
    /// The path the bytes are stored under. Generated, never the user's name: that removes path
    /// traversal, collisions and the chance of a name leaking something about the file's contents.
    /// </summary>
    public static string BuildBlobName(string fileName, DateTimeOffset now, Guid id)
    {
        var extension = Path.GetExtension(fileName);
        var safeExtension = extension.Length is > 1 and <= 10 && extension[1..].All(char.IsLetterOrDigit)
            ? extension.ToLowerInvariant()
            : "";

        return $"{now:yyyy}/{now:MM}/{id:N}{safeExtension}";
    }

    /// <summary>Drops any <c>; charset=…</c> the browser attached.</summary>
    private static string Normalise(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return "";
        }

        var semicolon = contentType.IndexOf(';', StringComparison.Ordinal);
        return (semicolon < 0 ? contentType : contentType[..semicolon]).Trim().ToLowerInvariant();
    }
}
