//when the button someone pressed has gone from the page (its panel closed once the step was done),
//focus would fall back to the very top of the document. this starts keyboard users again at the
//top of the page content instead. it waits a frame so the page has redrawn first
export function keepFocusOnPage() {
  requestAnimationFrame(() => {
    if (!document.activeElement || document.activeElement === document.body) {
      document.getElementById('main')?.focus();
    }
  });
}
