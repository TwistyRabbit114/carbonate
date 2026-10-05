import type { ReactNode } from 'react';
import styles from './FieldList.module.scss';

//label and value rows on a detail page, as a description list

export function FieldList({ children }: { children: ReactNode }) {
  return <dl className={styles.list}>{children}</dl>;
}

type FieldRowProps = {
  label: string;
  children?: ReactNode;
};

//no value, no row. that's what keeps a field the api left out (a masked price, say) off the page
//instead of showing an empty or zero row
export function FieldRow({ label, children }: FieldRowProps) {
  if (children === undefined || children === null || children === false) return null;

  return (
    <div className={styles.row}>
      <dt className={styles.label}>{label}</dt>
      <dd className={styles.value}>{children}</dd>
    </div>
  );
}
