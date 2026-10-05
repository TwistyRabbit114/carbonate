import DOMPurify from 'dompurify';

//card descriptions are the one field allowed formatting (plan section 7.5). people type a little
//markup in a plain textarea, **bold**, _italic_, "- " and "1. " lists and [text](https://link),
//and it's stored as html. no contenteditable, so nothing here ever reads or writes innerHTML

//----------------------------------------------------------\\
//                              CLEANING
//----------------------------------------------------------\\

//the same allowlist the api cleans against on the way in
const allowedTags = ['p', 'br', 'strong', 'em', 'ul', 'ol', 'li', 'a'];
const safeLink = /^(?:https:|mailto:)/i;

const purifier = DOMPurify(window);
const config = {
  ALLOWED_TAGS: allowedTags,
  ALLOWED_ATTR: ['href'],
  ALLOWED_URI_REGEXP: safeLink,
};

//links open on their own tab and can't reach back into this one
purifier.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('rel', 'noopener noreferrer');
    node.setAttribute('target', '_blank');
  }
});

//the api has cleaned it already, this is the second line in case anything slipped past
export function sanitiseDescription(html: string): string {
  return purifier.sanitize(html, config);
}

//----------------------------------------------------------\\
//                              MARKUP TO HTML
//----------------------------------------------------------\\

//the five characters html treats specially, so typed text can never turn into markup
const escapes: Record<string, string> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};
const escapeHtml = (text: string) => text.replace(/[&<>"']/g, (char) => escapes[char] ?? char);

const bulletLine = /^\s*[-*]\s+(.*)$/;
const numberLine = /^\s*\d+[.)]\s+(.*)$/;
const linkMarkup = /\[([^\]\n]+)\]\(([^)\s]+)\)/g;

//links are set aside behind a marker while bold and italic run, so an underscore in an address
//can't be read as italic. the marker is a private-use character, and any in the text is stripped first
const marker = '';
const anyMarker = //g;
const setAside = /(\d+)/g;

function inline(text: string): string {
  const links: string[] = [];
  const held = escapeHtml(text.replace(anyMarker, '')).replace(
    linkMarkup,
    (whole, label: string, href: string) => {
      if (!safeLink.test(href)) return whole;
      links.push(`<a href="${href}">${label}</a>`);
      return `${marker}${links.length - 1}${marker}`;
    },
  );

  return held
    .replace(/\*\*(?=\S)([\s\S]*?\S)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|[^\w])_(?=\S)([^_]*?\S)_(?!\w)/g, '$1<em>$2</em>')
    .replace(/\n/g, '<br>')
    .replace(setAside, (_, index: string) => links[Number(index)] ?? '');
}

type Block = { kind: 'p'; lines: string[] } | { kind: 'ul' | 'ol'; items: string[] };

//blank lines split paragraphs, and a run of "- " or "1. " lines is a list
function blocksOf(text: string): Block[] {
  const blocks: Block[] = [];
  let open: Block | undefined; //the block the next line can join

  for (const line of text.replace(/\r\n?/g, '\n').split('\n')) {
    const bullet = bulletLine.exec(line);
    const number = bullet ? null : numberLine.exec(line);
    const listKind = bullet ? 'ul' : number ? 'ol' : null;

    if (line.trim() === '') {
      open = undefined;
    } else if (listKind) {
      const item = (bullet ?? number)![1]!.trim();
      if (open && open.kind !== 'p' && open.kind === listKind) {
        open.items.push(item);
      } else {
        open = { kind: listKind, items: [item] };
        blocks.push(open);
      }
    } else if (open && open.kind === 'p') {
      open.lines.push(line.trim());
    } else {
      open = { kind: 'p', lines: [line.trim()] };
      blocks.push(open);
    }
  }

  return blocks;
}

//null when there's nothing but whitespace, so an empty description is cleared rather than saved
export function markupToHtml(text: string): string | null {
  const html = blocksOf(text)
    .map((block) =>
      block.kind === 'p'
        ? `<p>${inline(block.lines.join('\n'))}</p>`
        : `<${block.kind}>${block.items.map((item) => `<li>${inline(item)}</li>`).join('')}</${block.kind}>`,
    )
    .join('');
  return html || null;
}

//----------------------------------------------------------\\
//                              HTML TO MARKUP
//----------------------------------------------------------\\

function inlineMarkup(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) return node.textContent ?? '';
  if (!(node instanceof Element)) return '';

  const inner = Array.from(node.childNodes, inlineMarkup).join('');
  switch (node.tagName) {
    case 'BR':
      return '\n';
    case 'STRONG':
      return inner.trim() ? `**${inner}**` : inner;
    case 'EM':
      return inner.trim() ? `_${inner}_` : inner;
    case 'A': {
      const href = node.getAttribute('href');
      return href ? `[${inner}](${href})` : inner;
    }
    default:
      return inner;
  }
}

//a stored description back into the markup the editor shows, cleaned on the way so nothing
//outside the allowlist is ever walked
export function htmlToMarkup(html: string | null): string {
  if (!html) return '';
  const fragment = purifier.sanitize(html, { ...config, RETURN_DOM_FRAGMENT: true });

  const blocks: string[] = [];
  let loose = '';
  const flushLoose = () => {
    if (loose.trim()) blocks.push(loose.trim());
    loose = '';
  };

  for (const node of Array.from(fragment.childNodes)) {
    if (node instanceof Element && (node.tagName === 'UL' || node.tagName === 'OL')) {
      flushLoose();
      const items = Array.from(node.children).filter((child) => child.tagName === 'LI');
      blocks.push(
        items
          .map(
            (item, index) => `${node.tagName === 'UL' ? '-' : `${index + 1}.`} ${inlineMarkup(item).trim()}`,
          )
          .join('\n'),
      );
    } else if (node instanceof Element && node.tagName === 'P') {
      flushLoose();
      const text = inlineMarkup(node).trim();
      if (text) blocks.push(text);
    } else {
      loose += inlineMarkup(node);
    }
  }
  flushLoose();

  return blocks.join('\n\n');
}

//----------------------------------------------------------\\
//                              TOOLBAR EDITS
//----------------------------------------------------------\\

//what a toolbar button does to the textarea: the new text and what to select afterwards
export type MarkupEdit = { text: string; start: number; end: number };

//wraps the selection in a marker, or unwraps it when it's already wrapped. with nothing selected
//it puts in a placeholder, selected, ready to type over
export function wrapSelection(
  text: string,
  start: number,
  end: number,
  marker: string,
  placeholder: string,
): MarkupEdit {
  //spaces at the edges stay outside the markers, "**word **" isn't bold
  while (start < end && /\s/.test(text[start]!)) start++;
  while (end > start && /\s/.test(text[end - 1]!)) end--;

  const before = text.slice(0, start);
  const after = text.slice(end);
  if (before.endsWith(marker) && after.startsWith(marker)) {
    return {
      text: before.slice(0, -marker.length) + text.slice(start, end) + after.slice(marker.length),
      start: start - marker.length,
      end: end - marker.length,
    };
  }

  const selected = text.slice(start, end) || placeholder;
  return {
    text: `${before}${marker}${selected}${marker}${after}`,
    start: start + marker.length,
    end: start + marker.length + selected.length,
  };
}

//turns the selected lines into a list, or back into plain lines when they all already are one
export function toggleList(text: string, start: number, end: number, kind: 'bullet' | 'number'): MarkupEdit {
  const lineStart = text.lastIndexOf('\n', start - 1) + 1;
  const nextBreak = text.indexOf('\n', Math.max(end - 1, start));
  const lineEnd = nextBreak === -1 ? text.length : nextBreak;

  const lines = text.slice(lineStart, lineEnd).split('\n');
  const pattern = kind === 'bullet' ? bulletLine : numberLine;
  const filled = lines.filter((line) => line.trim() !== '');
  const alreadyList = filled.length > 0 && filled.every((line) => pattern.test(line));

  let number = 0;
  const changed = lines.map((line) => {
    if (line.trim() === '') return line;
    if (alreadyList) return pattern.exec(line)![1]!;
    const plain = (bulletLine.exec(line) ?? numberLine.exec(line))?.[1] ?? line.trim();
    number++;
    return kind === 'bullet' ? `- ${plain}` : `${number}. ${plain}`;
  });

  const block = changed.join('\n');
  return {
    text: text.slice(0, lineStart) + block + text.slice(lineEnd),
    start: lineStart,
    end: lineStart + block.length,
  };
}

//the selection becomes the link text and the address is selected to type over. a selected
//address goes the other way round
export function insertLink(text: string, start: number, end: number): MarkupEdit {
  const selected = text.slice(start, end).trim();
  const before = text.slice(0, start);
  const after = text.slice(end);

  if (safeLink.test(selected)) {
    const label = 'link text';
    return {
      text: `${before}[${label}](${selected})${after}`,
      start: start + 1,
      end: start + 1 + label.length,
    };
  }

  const label = selected || 'link text';
  const address = 'https://';
  const addressStart = start + label.length + 3;
  return {
    text: `${before}[${label}](${address})${after}`,
    start: addressStart,
    end: addressStart + address.length,
  };
}
