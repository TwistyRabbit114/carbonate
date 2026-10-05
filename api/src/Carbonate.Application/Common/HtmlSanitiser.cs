namespace Carbonate.Application.Common;

/// <summary>
/// Cleans the one field that may carry formatting, <c>TaskCard.Description</c> (plan section 7.5). It runs
/// on every write; the SPA cleans again with DOMPurify when it renders.
/// </summary>
public interface IHtmlSanitiser
{
    string Sanitise(string html);
}
