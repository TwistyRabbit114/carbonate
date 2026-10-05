namespace Carbonate.Application.Features.Venues;

internal static class FieldText
{
    //blank optional text is stored as null rather than an empty string
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
