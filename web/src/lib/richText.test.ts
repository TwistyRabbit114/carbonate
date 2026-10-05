import { describe, expect, it } from 'vitest';
import {
  htmlToMarkup,
  insertLink,
  markupToHtml,
  sanitiseDescription,
  toggleList,
  wrapSelection,
} from './richText';

//----------------------------------------------------------\\
//                              MARKUP TO HTML
//----------------------------------------------------------\\

describe('markupToHtml', () => {
  it('makes a paragraph per blank-line break and keeps single line breaks', () => {
    expect(markupToHtml('Book the inspector.\nBring the old certificate.\n\nThen file it.')).toBe(
      '<p>Book the inspector.<br>Bring the old certificate.</p><p>Then file it.</p>',
    );
  });

  it('escapes anything that looks like markup', () => {
    expect(markupToHtml('<img src=x onerror="alert(1)"> & more')).toBe(
      '<p>&lt;img src=x onerror=&quot;alert(1)&quot;&gt; &amp; more</p>',
    );
  });

  it('treats blank or whitespace-only text as no description', () => {
    expect(markupToHtml('   \n\n  ')).toBeNull();
  });

  it('copes with windows line endings', () => {
    expect(markupToHtml('one\r\ntwo\r\n\r\nthree')).toBe('<p>one<br>two</p><p>three</p>');
  });

  it('turns bold and italic markers into tags', () => {
    expect(markupToHtml('Call **security** first, _before_ load-in')).toBe(
      '<p>Call <strong>security</strong> first, <em>before</em> load-in</p>',
    );
  });

  it('leaves underscores inside words alone', () => {
    expect(markupToHtml('see bar_kit_list and **this**')).toBe(
      '<p>see bar_kit_list and <strong>this</strong></p>',
    );
  });

  it('makes lists from dashes and numbers, even straight after a line of text', () => {
    expect(markupToHtml('Bring:\n- ice\n- cups\n\n1. load\n2. strike')).toBe(
      '<p>Bring:</p><ul><li>ice</li><li>cups</li></ul><ol><li>load</li><li>strike</li></ol>',
    );
  });

  it('links https and mailto addresses, and keeps underscores in them', () => {
    expect(markupToHtml('[portal](https://example.com/a_b_c) or [mail](mailto:ops@example.com)')).toBe(
      '<p><a href="https://example.com/a_b_c">portal</a> or <a href="mailto:ops@example.com">mail</a></p>',
    );
  });

  it('never makes a link that runs script or breaks out of its attribute', () => {
    expect(markupToHtml('[x](javascript:alert(1))')).toBe('<p>[x](javascript:alert(1))</p>');
    expect(markupToHtml('[x](https://a.example/"onmouseover="alert(1))')).not.toContain('" onmouseover');
  });
});

//----------------------------------------------------------\\
//                              CLEANING AND BACK AGAIN
//----------------------------------------------------------\\

describe('sanitiseDescription', () => {
  it('drops scripts, handlers and tags outside the allowlist', () => {
    const clean = sanitiseDescription(
      '<p onclick="x()">Hi<img src=x onerror="alert(1)"><script>alert(1)</script></p>',
    );
    expect(clean).toBe('<p>Hi</p>');
  });

  it('keeps https links, opens them in a new tab, and strips unsafe ones', () => {
    expect(sanitiseDescription('<a href="https://example.com">map</a>')).toBe(
      '<a href="https://example.com" rel="noopener noreferrer" target="_blank">map</a>',
    );
    expect(sanitiseDescription('<a href="javascript:alert(1)">x</a>')).not.toContain('javascript');
  });
});

describe('htmlToMarkup', () => {
  it('gives back the markup a description was written in', () => {
    const markup =
      'Call **security** _first_\nthen [map](https://example.com)\n\n- ice\n- cups\n\n1. load\n2. strike';
    expect(htmlToMarkup(markupToHtml(markup))).toBe(markup);
  });

  it('reads loose text the api stored outside a paragraph, and nothing at all as empty', () => {
    expect(htmlToMarkup('Just text<br>on two lines')).toBe('Just text\non two lines');
    expect(htmlToMarkup(null)).toBe('');
  });
});

//----------------------------------------------------------\\
//                              TOOLBAR EDITS
//----------------------------------------------------------\\

describe('toolbar edits', () => {
  it('wraps the selection, leaving spaces at its edges outside the markers', () => {
    expect(wrapSelection('call security now', 4, 14, '**', 'bold text')).toEqual({
      text: 'call **security** now',
      start: 7,
      end: 15,
    });
  });

  it('unwraps a selection that is already wrapped', () => {
    expect(wrapSelection('call **security** now', 7, 15, '**', 'bold text').text).toBe('call security now');
  });

  it('puts in a selected placeholder when nothing is selected', () => {
    expect(wrapSelection('x ', 2, 2, '_', 'italic text')).toEqual({
      text: 'x _italic text_',
      start: 3,
      end: 14,
    });
  });

  it('turns lines into a numbered list, and back', () => {
    const listed = toggleList('Steps\nload\nstrike', 7, 14, 'number');
    expect(listed.text).toBe('Steps\n1. load\n2. strike');
    expect(toggleList(listed.text, listed.start, listed.end, 'number').text).toBe('Steps\nload\nstrike');
  });

  it('makes the selection the link text and selects the address to type over', () => {
    const edit = insertLink('see map', 4, 7);
    expect(edit.text).toBe('see [map](https://)');
    expect(edit.text.slice(edit.start, edit.end)).toBe('https://');
  });
});
