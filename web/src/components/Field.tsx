import { useId, type ReactNode } from 'react';
import { cx } from '@/lib/cx';
import styles from './Field.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

export type FieldControlProps = {
  id: string;
  'aria-invalid'?: true;
  'aria-describedby'?: string;
};

type FieldProps = {
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  full?: boolean;
  //render prop, so the control gets the id and aria wiring without cloneElement
  children: (control: FieldControlProps) => ReactNode;
};

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

export function Field({ label, hint, error, required = false, full = false, children }: FieldProps) {
  const id = useId();
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  return (
    <div className={cx(styles.field, full && styles.full)}>
      <label htmlFor={id} className={styles.label}>
        {label}
        {required && <span className={styles.required}> (required)</span>}
      </label>
      {children({ id, 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy })}
      {hint && (
        <p id={hintId} className={styles.hint}>
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className={styles.error}>
          {error}
        </p>
      )}
    </div>
  );
}
