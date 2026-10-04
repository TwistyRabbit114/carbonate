import type { ReactNode } from 'react';
import styles from './Kanban.module.scss';

type KanbanBoardProps = {
  label: string;
  children: ReactNode; //KanbanColumn elements
};

//columns side by side, scrolling sideways on tablets and stacking on phones
export function KanbanBoard({ label, children }: KanbanBoardProps) {
  return (
    <div className={styles.board} role="group" aria-label={label}>
      {children}
    </div>
  );
}
