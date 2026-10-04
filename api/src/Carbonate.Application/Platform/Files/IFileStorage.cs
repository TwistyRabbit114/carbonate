namespace Carbonate.Application.Platform.Files;

/// <summary>
/// The one way files leave or enter Carbonate (FR-31, and FR-06 and FR-22 depend on it).
/// </summary>
/// <remarks>
/// Everything is private. Nothing is ever served from a public container, and nothing is served
/// inline — downloads go through a short-lived link with <c>Content-Disposition: attachment</c>, so an
/// uploaded <c>.html</c> or <c>.svg</c> cannot execute in the application's origin (Task 1 §7.5).
/// </remarks>
public interface IFileStorage
{
    /// <summary>
    /// Validates the file, stores it under a generated name and returns where it went.
    /// </summary>
    /// <param name="content">Read to the end. Does not need to be seekable.</param>
    /// <param name="fileName">The user's name for the file. Used for the extension check and the
    /// download name only — never as the storage path.</param>
    /// <param name="contentType">The declared type, checked against the allow-list.</param>
    /// <exception cref="Common.ProblemException">400 when the type or size is not allowed.</exception>
    Task<StoredFile> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>Opens the stored file for reading. For server-side work, not for serving to a browser.</summary>
    Task<Stream> OpenReadAsync(string blobUri, CancellationToken ct = default);

    /// <summary>
    /// A short-lived read-only link the browser can follow, forcing a download under
    /// <paramref name="downloadFileName"/>.
    /// </summary>
    /// <remarks>
    /// Async because a user-delegation SAS is signed with a key fetched from Azure AD — the point of
    /// which is that no storage account key exists anywhere to sign it with.
    /// </remarks>
    Task<Uri> GetDownloadUrlAsync(
        string blobUri,
        string downloadFileName,
        CancellationToken ct = default);
}

/// <param name="BlobUri">What goes in the database column. Not a link anyone can follow.</param>
/// <param name="Sha256">Lower-case hex. Lets a later read prove the bytes are the ones uploaded.</param>
public readonly record struct StoredFile(string BlobUri, string Sha256, long SizeBytes, string ContentType);
