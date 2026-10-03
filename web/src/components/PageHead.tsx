import type { ReactNode } from 'react';
import styles from './PageHead.module.scss';

type PageHeadProps = {
  title: string;
  eyebrow?: ReactNode;
  actions?: ReactNode;
};

//every page gets one h1 and a matching tab title, react hoists <title> into the head
export function PageHead({ title, eyebrow, actions }: PageHeadProps) {
  return (
    <div className={styles.head}>
      <title>{`${title} — Carbonate`}</title>
      <div>
        {eyebrow && <p className={styles.eyebrow}>{eyebrow}</p>}
        <h1>{title}</h1>
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </div>
  );
}
