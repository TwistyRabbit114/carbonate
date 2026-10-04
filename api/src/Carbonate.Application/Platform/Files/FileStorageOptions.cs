namespace Carbonate.Application.Platform.Files;

public class FileStorageOptions
{
    public const string Section = "Storage";

    /// <summary>
    /// The blob service endpoint, e.g. <c>https://stcarbonate.blob.core.windows.net</c>. No key and no
    /// SAS: the App Service connects with its managed identity (docs/hosting.md).
    /// </summary>
    public string BlobServiceUri { get; set; } = "";

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
