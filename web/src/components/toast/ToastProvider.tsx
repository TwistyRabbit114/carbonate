import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import { CircleAlert, CircleCheck, Info, X } from 'lucide-react';
import { cx } from '@/lib/cx';
import { ToastContext } from './ToastContext';
import styles from './Toast.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type Tone = 'success' | 'info' | 'error';

type Toast = {
  id: number;
  tone: Tone;
  message: string;
};

const icons = { success: CircleCheck, info: Info, error: CircleAlert };
const dismissAfterMs = 6000;
const maxShown = 4;

//----------------------------------------------------------\\
//                              PROVIDER
//----------------------------------------------------------\\

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((shown) => shown.filter((toast) => toast.id !== id));
  }, []);

  const show = useCallback(
    (tone: Tone, message: string) => {
      const id = ++nextId.current;
      setToasts((shown) => [...shown.slice(-(maxShown - 1)), { id, tone, message }]);

      //errors stay until closed, people need time to read why something didn't happen
      if (tone !== 'error') window.setTimeout(() => dismiss(id), dismissAfterMs);
    },
    [dismiss],
  );

  const api = useMemo(
    () => ({
      success: (message: string) => show('success', message),
      info: (message: string) => show('info', message),
      error: (message: string) => show('error', message),
    }),
    [show],
  );

  //both live regions are always in the page, screen readers only announce changes to ones already there.
  //aria-live alone, no roles, so an empty region never passes for a real status or alert on the page
  return (
    <ToastContext value={api}>
      {children}
      <div className={styles.region}>
        <div aria-live="polite" className={styles.stack}>
          {toasts
            .filter((toast) => toast.tone !== 'error')
            .map((toast) => (
              <ToastView key={toast.id} toast={toast} onDismiss={dismiss} />
            ))}
        </div>
        <div aria-live="assertive" className={styles.stack}>
          {toasts
            .filter((toast) => toast.tone === 'error')
            .map((toast) => (
              <ToastView key={toast.id} toast={toast} onDismiss={dismiss} />
            ))}
        </div>
      </div>
    </ToastContext>
  );
}

//----------------------------------------------------------\\
//                              TOAST
//----------------------------------------------------------\\

function ToastView({ toast, onDismiss }: { toast: Toast; onDismiss: (id: number) => void }) {
  const Icon = icons[toast.tone];

  return (
    <div className={cx(styles.toast, styles[toast.tone])}>
      <Icon className={styles.icon} aria-hidden="true" />
      <p className={styles.message}>{toast.message}</p>
      <button type="button" className={styles.close} onClick={() => onDismiss(toast.id)} aria-label="Dismiss">
        <X aria-hidden="true" />
      </button>
    </div>
  );
}
