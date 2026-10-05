import { Logo } from '@/components/Logo';
import styles from './Centered.module.scss';

//the moment between page load and the refresh cookie answering. short, so just the logo
export function SessionLoading() {
  return (
    <main className={styles.page} aria-busy="true">
      <div className={styles.loading}>
        <Logo size={56} />
        <span role="status">Loading Carbonate…</span>
      </div>
    </main>
  );
}
