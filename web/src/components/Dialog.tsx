import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import styles from './Dialog.module.scss';

type DialogProps = {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
};

//native <dialog> opened with showModal, so focus is trapped inside and esc closes it.
//it only exists in the page while open
export function Dialog({ open, title, onClose, children }: DialogProps) {
  if (!open) return null;
  return (
    <OpenDialog title={title} onClose={onClose}>
      {children}
    </OpenDialog>
  );
}

function OpenDialog({ title, onClose, children }: Omit<DialogProps, 'open'>) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  //whatever had focus when the dialog opened, read before showModal moves focus inside
  const [opener] = useState(() => document.activeElement as HTMLElement | null);

  useEffect(() => {
    //strict mode runs this twice, so only open it if it isn't already
    const dialog = ref.current;
    if (dialog && !dialog.open) dialog.showModal();

    //taking an open dialog out of the page leaves focus on the body, so hand it back to the
    //opener. if the opener has gone (a card that just moved), the page puts focus somewhere sensible
    return () => {
      if (opener?.isConnected) opener.focus();
    };
  }, [opener]);

  return (
    <dialog ref={ref} className={styles.dialog} aria-labelledby={titleId} onClose={onClose}>
      <h2 id={titleId} className={styles.title}>
        {title}
      </h2>
      {children}
    </dialog>
  );
}
