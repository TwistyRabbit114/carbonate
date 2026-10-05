import { useSyncExternalStore } from 'react';

//true while the media query matches. falls back to false where matchMedia doesn't exist (tests)
export function useMediaQuery(query: string) {
  return useSyncExternalStore(
    (onChange) => {
      const list = window.matchMedia?.(query);
      list?.addEventListener('change', onChange);
      return () => list?.removeEventListener('change', onChange);
    },
    () => window.matchMedia?.(query).matches ?? false,
    () => false,
  );
}

//same breakpoint as the phone mixin in styles/mixins.scss
export const phoneQuery = '(max-width: 760px)';
