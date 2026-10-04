using Carbonate.Application.Common;
using Carbonate.Application.Platform.Files;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

/// <summary>FR-31: what Carbonate will and will not accept as an upload.</summary>
public class FileValidationTests
{
    [Theory]
    [InlineData("rack.jpg", "image/jpeg")]
    [InlineData("rack.jpeg", "image/jpeg")]
    [InlineData("rack.PNG", "image/png")]
    [InlineData("rack.webp", "image/webp")]
    [InlineData("photo.heic", "image/heic")]
    [InlineData("hs-file.pdf", "application/pdf")]
    // Browsers attach a charset to some types; it is not part of the decision.
    [InlineData("hs-file.pdf", "application/pdf; charset=utf-8")]
    public void Accepts_the_allowed_types(string fileName, string contentType) =>
        Should.NotThrow(() => FileValidation.ValidateType(fileName, contentType));

    [Theory]
    // The two that matter: anything the browser might execute in our origin.
    [InlineData("payload.html", "text/html")]
    [InlineData("payload.svg", "image/svg+xml")]
    [InlineData("macro.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("tool.exe", "application/octet-stream")]
    [InlineData("rack.jpg", "")]
    public void Rejects_everything_else(string fileName, string contentType) =>
        Should.Throw<ProblemException>(() => FileValidation.ValidateType(fileName, contentType))
            .Status.ShouldBe(400);

    [Fact]
    public void Rejects_a_file_whose_extension_disagrees_with_its_type()
    {
        // A PDF announced as a PNG is either a mistake or an attempt. Neither gets stored.
        Should.Throw<ProblemException>(() => FileValidation.ValidateType("invoice.pdf", "image/png"))
            .Status.ShouldBe(400);
    }

    [Fact]
    public void Rejects_a_file_with_no_extension() =>
        Should.Throw<ProblemException>(() => FileValidation.ValidateType("photo", "image/jpeg"));

    [Fact]
    public void Returns_the_normalised_type() =>
        FileValidation.ValidateType("a.JPG", "IMAGE/JPEG").ShouldBe("image/jpeg");

    [Fact]
    public void Accepts_a_file_inside_the_cap() =>
        Should.NotThrow(() => FileValidation.ValidateSize(10 * 1024 * 1024, 10 * 1024 * 1024));

    [Fact]
    public void Rejects_a_file_one_byte_over_the_cap() =>
        Should.Throw<ProblemException>(() => FileValidation.ValidateSize(10 * 1024 * 1024 + 1, 10 * 1024 * 1024))
            .Status.ShouldBe(400);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_an_empty_file(long size) =>
        Should.Throw<ProblemException>(() => FileValidation.ValidateSize(size, 10 * 1024 * 1024));

    [Theory]
    // Path separators, quotes and traversal are stripped: this string goes into a response header.
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\me\\rack.jpg", "rack.jpg")]
    [InlineData("na\"me.jpg", "name.jpg")]
    [InlineData("", "download")]
    [InlineData("   ", "download")]
    public void SafeDownloadName_strips_anything_dangerous(string input, string expected) =>
        FileValidation.SafeDownloadName(input).ShouldBe(expected);

    [Fact]
    public void SafeDownloadName_drops_control_characters() =>
        FileValidation.SafeDownloadName("rack\r\n.jpg").ShouldBe("rack.jpg");

    [Fact]
    public void Blob_name_is_generated_not_taken_from_the_user()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        var name = FileValidation.BuildBlobName("../../secret.jpg", now, id);

        // Date-partitioned for housekeeping, GUID-named so nothing of the user's input survives
        // except a checked extension.
        name.ShouldBe("2026/10/11111111222233334444555555555555.jpg");
    }

    [Theory]
    [InlineData("no-extension")]
    [InlineData("odd.j pg")]
    [InlineData("trailing.")]
    public void Blob_name_drops_an_extension_it_does_not_trust(string fileName)
    {
        var name = FileValidation.BuildBlobName(fileName, DateTimeOffset.UtcNow, Guid.NewGuid());

        Path.GetExtension(name).ShouldBeEmpty();
    }
}
