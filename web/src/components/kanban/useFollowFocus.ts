import { useEffect, useState } from 'react';

//where keyboard focus should land once a moved card has settled. the selector points at one of
//the card's buttons, which carry data-card-id and data-control. the fallback is for a card that
//arrives without that button, because the user has nothing left to do with it
export type FocusPlan = { cardId: string; selector: string; fallback?: string };

//a moved card is drawn fresh in its new column, which drops keyboard focus on the page body.
//this puts it back on the same button of that card, and again if a rejected move sends it home.
//it only ever picks focus up off the body, never takes it from something the user chose.
//redrawn is whatever changes when the board does, so the check runs after each redraw
export function useFollowFocus(redrawn: unknown) {
  const [plan, setPlan] = useState<FocusPlan | null>(null);

  useEffect(() => {
    if (!plan) return;
    const target =
      document.querySelector<HTMLElement>(plan.selector) ??
      (plan.fallback ? document.querySelector<HTMLElement>(plan.fallback) : null);
    const focusLost = !document.activeElement || document.activeElement === document.body;
    if (target && focusLost) target.focus();
  }, [plan, redrawn]);

  //stop following the card once the user clicks or tabs somewhere else
  useEffect(() => {
    if (!plan) return;
    const stop = (event: Event) => {
      const element = event.target instanceof HTMLElement ? event.target : null;
      const ours = element?.dataset.cardId === plan.cardId || element?.matches(plan.selector);
      if (event.type === 'pointerdown' || !ours) setPlan(null);
    };
    document.addEventListener('focusin', stop);
    document.addEventListener('pointerdown', stop);
    return () => {
      document.removeEventListener('focusin', stop);
      document.removeEventListener('pointerdown', stop);
    };
  }, [plan]);

  return setPlan;
}
