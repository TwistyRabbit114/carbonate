namespace Carbonate.Application.Platform.Files;

public class FileStorageOptions
{
    public const string Section = "Storage";

    /// <summary>
    /// The blob service endpoint, e.g. <c>https://stcarbonate.blob.core.windows.net</c>. No key and no
    /// SAS: the App Service connects with its managed identity (docs/hosting.md).
    /// </summary>
    public string BlobServiceUri { get; set; } = "";

    /// <summary>
    /// Local development only. The Azurite emulator speaks plain HTTP, and
    /// <c>DefaultAzureCredential</c> refuses to send a bearer token over an unencrypted connection, so
    /// every upload and download fails with a 500 against the emulator. When this is set it takes
    /// precedence over <see cref="BlobServiceUri"/> and the client authenticates with the account key
    /// in the string instead.
    /// <para>
    /// Never set in Azure. Both App Services connect with a managed identity and no account key
    /// exists — see docs/hosting.md. A connection string here would be a secret in configuration,
    /// which is exactly what the hosting design avoids.
    /// </para>
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>True when the emulator path above is configured.</summary>
    public bool UsesConnectionString => !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>Private, with no anonymous access. Creating it is a deployment step, not the app's job.</summary>
    public string Container { get; set; } = "uploads";

    /// <summary>10 MB (FR-31). A phone photo of a broken glass rack is well under this.</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// How long a download link stays valid. Short on purpose: a link pasted into a group chat should
    /// stop working quickly.
    /// </summary>
    public int DownloadLinkMinutes { get; set; } = 10;
}
