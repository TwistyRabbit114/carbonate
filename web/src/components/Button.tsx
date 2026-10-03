import type { ButtonHTMLAttributes } from 'react';
import { LoaderCircle } from 'lucide-react';
import { cx } from '@/lib/cx';
import styles from './Button.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'default' | 'ghost' | 'danger';
  busy?: boolean;
  block?: boolean;
};

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

export function Button({
  variant = 'default',
  busy = false,
  block = false,
  type = 'button',
  className,
  disabled,
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cx(
        styles.button,
        variant !== 'default' && styles[variant],
        block && styles.block,
        className,
      )}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      {...rest}
    >
      {busy && <LoaderCircle className={styles.spinner} aria-hidden="true" />}
      {children}
    </button>
  );
}
