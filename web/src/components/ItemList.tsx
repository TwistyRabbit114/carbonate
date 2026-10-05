import type { ReactNode } from 'react';
import styles from './ItemList.module.scss';

//a list of records inside a panel (milestones, shifts, incidents), each with a bold line, a
//muted line under it, and room for a badge and a button. the same look on every detail page

export function ItemList({ label, children }: { label?: string; children: ReactNode }) {
  return (
    <ul className={styles.list} aria-label={label}>
      {children}
    </ul>
  );
}

type ItemProps = {
  title: ReactNode;
  badge?: ReactNode;
  meta?: ReactNode; //the muted line under the title
  actions?: ReactNode;
  children?: ReactNode; //anything longer, under the meta line
};

export function Item({ title, badge, meta, actions, children }: ItemProps) {
  return (
    <li className={styles.item}>
      <div className={styles.main}>
        <p className={styles.title}>
          <span>{title}</span>
          {badge}
        </p>
        {meta && <p className={styles.meta}>{meta}</p>}
        {children && <div className={styles.body}>{children}</div>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </li>
  );
}
