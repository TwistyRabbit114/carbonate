import axe from 'axe-core';

//jsdom can't work out colours, so contrast is covered by the token ratios and lighthouse instead.
//violations come back as short strings so a failing test says what broke
export async function axeViolations(container: Element) {
  const results = await axe.run(container, { rules: { 'color-contrast': { enabled: false } } });
  return results.violations.map((violation) => `${violation.id}: ${violation.help}`);
}
