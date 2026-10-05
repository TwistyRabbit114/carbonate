using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Carbonate.Application.Common;
using Carbonate.Application.Platform.Files;
using Microsoft.Extensions.Options;

namespace Carbonate.Infrastructure.Platform.Files;

/// <summary>
/// <see cref="IFileStorage"/> on Azure Blob Storage, reached with the App Service's managed identity.
/// There is no account key anywhere, which is why download links are signed with a user-delegation key.
/// </summary>
internal sealed class BlobFileStorage(
    BlobServiceClient service,
    IOptions<FileStorageOptions> options,
    TimeProvider clock) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<StoredFile> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var type = FileValidation.ValidateType(fileName, contentType);

        // Buffered rather than streamed straight through, because the hash has to be known before the
        // upload commits and the cap is 10 MB — small enough that the simple option is the right one.
        using var buffer = await ReadWithinCapAsync(content, _options.MaxBytes, ct);
        FileValidation.ValidateSize(buffer.Length, _options.MaxBytes);

        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, ct));
        buffer.Position = 0;

        var now = clock.GetUtcNow();
        var blob = Container().GetBlobClient(FileValidation.BuildBlobName(fileName, now, Guid.NewGuid()));

        await blob.UploadAsync(
            buffer,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = type,
                    // Set at rest as well as on the link, so the blob cannot be rendered inline even if
                    // it is ever reached by another route.
                    ContentDisposition = "attachment",
                },
                // Fails rather than overwrites. The name is a fresh GUID, so a collision means
                // something is wrong and silently replacing a file would be the worse outcome.
                Conditions = new BlobRequestConditions { IfNoneMatch = Azure.ETag.All },
            },
            ct);

        return new StoredFile(blob.Uri.ToString(), hash, buffer.Length, type);
    }

    public async Task<Stream> OpenReadAsync(string blobUri, CancellationToken ct = default) =>
        await Blob(blobUri).OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), ct);

    public async Task<Uri> GetDownloadUrlAsync(
        string blobUri,
        string downloadFileName,
        CancellationToken ct = default)
    {
        var blob = Blob(blobUri);
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.DownloadLinkMinutes);

        var builder = new BlobSasBuilder
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b",
            // A minute of leeway for clock drift between here and the storage service.
            StartsOn = now.AddMinutes(-1),
            ExpiresOn = expires,
            ContentDisposition = $"attachment; filename=\"{FileValidation.SafeDownloadName(downloadFileName)}\"",
        };
        builder.SetPermissions(BlobSasPermissions.Read);

        // Local development against Azurite: the client was built from a connection string, so it
        // holds a shared key and can sign the SAS itself. Azurite does not implement user delegation
        // keys at all, so the branch below cannot work there. Never taken in Azure — see
        // FileStorageOptions.ConnectionString.
        if (blob.CanGenerateSasUri)
        {
            return blob.GenerateSasUri(builder);
        }

        // Signed with a key Azure AD issues to the managed identity, not with an account key — there
        // is no account key. The delegation key cannot outlive the SAS it signs.
        var delegationKey = await service.GetUserDelegationKeyAsync(now.AddMinutes(-1), expires, ct);

        var sas = builder.ToSasQueryParameters(delegationKey.Value, service.AccountName).ToString();
        return new UriBuilder(blob.Uri) { Query = sas }.Uri;
    }

    private BlobContainerClient Container() => service.GetBlobContainerClient(_options.Container);

    /// <summary>Rejects a URI that points anywhere but this account's uploads container.</summary>
    private BlobClient Blob(string blobUri)
    {
        if (!Uri.TryCreate(blobUri, UriKind.Absolute, out var uri))
        {
            throw ProblemException.NotFound();
        }

        var container = Container();
        if (!uri.AbsoluteUri.StartsWith(container.Uri.AbsoluteUri + "/", StringComparison.Ordinal))
        {
            // A stored URI that is not ours is a tampered or corrupt row, not a file.
            throw ProblemException.NotFound();
        }

        var name = Uri.UnescapeDataString(uri.AbsoluteUri[(container.Uri.AbsoluteUri.Length + 1)..]);
        return container.GetBlobClient(name);
    }

    /// <summary>
    /// Copies up to <paramref name="maxBytes"/> + 1 so an oversized upload is refused after one extra
    /// byte rather than after the whole file has been read into memory.
    /// </summary>
    private static async Task<MemoryStream> ReadWithinCapAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
            {
                buffer.Dispose();
                FileValidation.ValidateSize(maxBytes + 1, maxBytes);
            }
        }

        buffer.Position = 0;
        return buffer;
    }
}
