import styles from './Kanban.module.scss';

type KanbanSkeletonProps = {
  columns: number;
};

//board-shaped placeholder while the first load is in flight
export function KanbanSkeleton({ columns }: KanbanSkeletonProps) {
  return (
    <div className={styles.board} aria-busy="true">
      <span className="visually-hidden" role="status">
        Loading the board…
      </span>
      {Array.from({ length: columns }, (_, column) => (
        <div key={column} className={styles.column} aria-hidden="true">
          <div className={styles.head}>
            <span className={styles.skeletonTitle} />
          </div>
          <div className={styles.body}>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </div>
        </div>
      ))}
    </div>
  );
}
