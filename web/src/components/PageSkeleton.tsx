import styles from './PageSkeleton.module.scss';

//placeholder shapes while a route chunk or its data loads, never a full-page spinner (NFR-06)
export function PageSkeleton() {
  return (
    <div className={styles.skeleton} aria-busy="true">
      <span className="visually-hidden" role="status">
        Loading…
      </span>
      <div className={styles.eyebrow} aria-hidden="true" />
      <div className={styles.title} aria-hidden="true" />
      <div className={styles.block} aria-hidden="true" />
      <div className={styles.block} aria-hidden="true" />
    </div>
  );
}
