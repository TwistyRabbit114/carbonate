using Azure.Core;
using Azure.Storage.Blobs;
using Carbonate.Application.Platform.Files;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

/// <summary>
/// FR-31. Which of the two ways of reaching Blob Storage is in play, and therefore which way a
/// download link gets signed.
/// <para>
/// In Azure the App Service connects with a managed identity, there is no account key, and links are
/// signed with a user delegation key. The Azurite emulator supports neither: it speaks plain HTTP,
/// which <c>DefaultAzureCredential</c> refuses to send a bearer token over, and it does not implement
/// user delegation keys. So local development uses a connection string and the shared key in it.
/// </para>
/// </summary>
public class FileStorageClientTests
{
    // Not the real Azurite key — any parseable base64 proves the branch, and a well-known key in the
    // repository invites someone to paste it somewhere that matters (plan section 14, rule 5).
    private const string LocalConnection =
        "DefaultEndpointsProtocol=http;" +
        "AccountName=devstoreaccount1;" +
        "AccountKey=TGVhdmUgdGhpcyBhbG9uZSwgaXQgaXMgYSB0ZXN0IGZpeHR1cmUu;" +
        "BlobEndpoint=http://127.0.0.1:10010/devstoreaccount1;";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_connection_string_means_the_cloud_path(string? value) =>
        new FileStorageOptions { ConnectionString = value }.UsesConnectionString.ShouldBeFalse();

    [Fact]
    public void A_configured_connection_string_means_the_emulator_path() =>
        new FileStorageOptions { ConnectionString = LocalConnection }.UsesConnectionString.ShouldBeTrue();

    [Fact]
    public void The_emulator_client_signs_its_own_download_links()
    {
        var blob = new BlobServiceClient(LocalConnection)
            .GetBlobContainerClient("uploads")
            .GetBlobClient("2026/10/photo.jpg");

        // The shared key is present, so GetDownloadUrlAsync takes the local branch and never asks for
        // a user delegation key — which Azurite would refuse.
        blob.CanGenerateSasUri.ShouldBeTrue();
    }

    [Fact]
    public void The_cloud_client_cannot_sign_and_must_use_a_delegation_key()
    {
        var blob = new BlobServiceClient(
                new Uri("https://stcarbonate.blob.core.windows.net"),
                new StubCredential())
            .GetBlobContainerClient("uploads")
            .GetBlobClient("2026/10/photo.jpg");

        // No account key exists in Azure. If this ever became true it would mean one had been
        // introduced into the configuration, which the hosting design forbids (docs/hosting.md).
        blob.CanGenerateSasUri.ShouldBeFalse();
    }

    [Fact]
    public void The_emulator_endpoint_is_loopback_so_the_csp_exception_is_safe()
    {
        var uri = new BlobServiceClient(LocalConnection).Uri;

        // Program.BlobHost allows a plain-HTTP blob host in the content security policy only when it
        // is loopback, so the exception cannot be pointed at a remote host.
        uri.IsLoopback.ShouldBeTrue();
        uri.Scheme.ShouldBe(Uri.UriSchemeHttp);
    }

    /// <summary>Never called: constructing the client makes no network request.</summary>
    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
