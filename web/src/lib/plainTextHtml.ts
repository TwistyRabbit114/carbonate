//the five characters html treats specially, so typed text can never turn into markup
const escapes: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
const escapeHtml = (text: string) => text.replace(/[&<>"']/g, (char) => escapes[char] ?? char);

//card descriptions are stored as sanitised html. typed text becomes a paragraph per blank-line
//break, with line breaks kept inside them. the api cleans it again on the way in either way
export function plainTextToHtml(text: string): string | null {
  const paragraphs = text
    .replace(/\r\n?/g, '\n')
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean);

  if (paragraphs.length === 0) return null;
  return paragraphs.map((paragraph) => `<p>${paragraph.split('\n').map(escapeHtml).join('<br>')}</p>`).join('');
}
