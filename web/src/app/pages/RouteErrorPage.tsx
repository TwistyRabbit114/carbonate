import { Button } from '@/components/Button';
import { Logo } from '@/components/Logo';
import styles from './Centered.module.scss';

//catches render crashes and failed route chunk loads (usually a deploy mid-session), no details shown
export function RouteErrorPage() {
  return (
    <main className={styles.page}>
      <title>Something went wrong · Carbonate</title>
      <div className={styles.card} role="alert">
        <Logo size={48} withName className={styles.logo} />
        <h1>Something went wrong</h1>
        <p>Reloading the page usually sorts it out.</p>
        <Button variant="primary" onClick={() => window.location.reload()}>
          Reload
        </Button>
      </div>
    </main>
  );
}
