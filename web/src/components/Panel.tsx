import { useId, type ReactNode } from 'react';
import styles from './Panel.module.scss';

type PanelProps = {
  title: string;
  actions?: ReactNode; //small buttons beside the heading
  children: ReactNode;
};

//a titled block on a detail page, named by its heading so screen readers can jump between them
export function Panel({ title, actions, children }: PanelProps) {
  const titleId = useId();

  return (
    <section className={styles.panel} aria-labelledby={titleId}>
      <div className={styles.head}>
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {actions && <div className={styles.actions}>{actions}</div>}
      </div>
      {children}
    </section>
  );
}
