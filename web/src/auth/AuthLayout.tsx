import { Suspense } from 'react';
import { Navigate, Outlet, useSearchParams } from 'react-router';
import { SessionLoading } from '@/app/pages/SessionLoading';
import { Logo } from '@/components/Logo';
import { useAuth } from './AuthContext';
import { afterLoginPath } from './redirect';
import styles from './Auth.module.scss';

//the stages everyone at carbon already talks in, drawn as a line along the bottom of the brand panel
const milestones = ['Recce', 'Load-in', 'Doors', 'Strike', 'Debrief'] as const;

//the login screens: a brand panel beside the form, folded into a band above it on phones. it's also
//the one place that redirects after login, so the screens inside only finish the session and never
//race each other to navigate
export function AuthLayout() {
  const { state } = useAuth();
  const [params] = useSearchParams();

  if (state.status === 'loading') return <SessionLoading />;
  if (state.status === 'signedIn') {
    return <Navigate to={afterLoginPath(params.get('next'), state.me)} replace />;
  }

  return (
    <div className={styles.page}>
      <header className={styles.brand}>
        <Logo size={52} withName className={styles.logo} />
        <div className={styles.pitch}>
          <p className={styles.tagline}>Every event, from booking to debrief.</p>
          <p className={styles.about}>
            One plan for Carbon Events and Carbon Logistics Management: the events board, admin tasks, stock and
            order lists.
          </p>
        </div>
        <ol className={styles.chain} aria-hidden="true">
          {milestones.map((name) => (
            <li key={name}>{name}</li>
          ))}
        </ol>
        <img src="/carbonate-logo.svg" alt="" className={styles.watermark} />
      </header>

      <main className={styles.main}>
        <div className={styles.panel}>
          <Suspense fallback={<p role="status">Loading…</p>}>
            <Outlet />
          </Suspense>
        </div>
      </main>
    </div>
  );
}
