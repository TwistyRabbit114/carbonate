import { describe, expect, it } from 'vitest';
import { plainTextToHtml } from './plainTextHtml';

describe('plainTextToHtml', () => {
  it('makes a paragraph per blank-line break and keeps single line breaks', () => {
    expect(plainTextToHtml('Book the inspector.\nBring the old certificate.\n\nThen file it.')).toBe(
      '<p>Book the inspector.<br>Bring the old certificate.</p><p>Then file it.</p>',
    );
  });

  it('escapes anything that looks like markup', () => {
    expect(plainTextToHtml('<img src=x onerror="alert(1)"> & more')).toBe(
      '<p>&lt;img src=x onerror=&quot;alert(1)&quot;&gt; &amp; more</p>',
    );
  });

  it('treats blank or whitespace-only text as no description', () => {
    expect(plainTextToHtml('   \n\n  ')).toBeNull();
  });

  it('copes with windows line endings', () => {
    expect(plainTextToHtml('one\r\ntwo\r\n\r\nthree')).toBe('<p>one<br>two</p><p>three</p>');
  });
});
