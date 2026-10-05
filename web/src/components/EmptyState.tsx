import type { ReactNode } from 'react';
import { Inbox, type LucideIcon } from 'lucide-react';
import styles from './EmptyState.module.scss';

type EmptyStateProps = {
  title: string;
  icon?: LucideIcon;
  children?: ReactNode;
};

//empty states say what happens next, not just that there's nothing here
export function EmptyState({ title, icon: Icon = Inbox, children }: EmptyStateProps) {
  return (
    <div className={styles.empty}>
      <Icon className={styles.icon} aria-hidden="true" />
      <h2 className={styles.title}>{title}</h2>
      {children && <div className={styles.body}>{children}</div>}
    </div>
  );
}
