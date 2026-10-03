import { Suspense } from 'react';
import { Navigate, Outlet, useSearchParams } from 'react-router';
import { SessionLoading } from '@/app/pages/SessionLoading';
import { Logo } from '@/components/Logo';
import { useAuth } from './AuthContext';
import { afterLoginPath } from './redirect';
import styles from './Auth.module.scss';

//the login card. it's also the one place that redirects after login, so the screens inside
//only finish the session and never race each other to navigate
export function AuthLayout() {
  const { state } = useAuth();
  const [params] = useSearchParams();

  if (state.status === 'loading') return <SessionLoading />;
  if (state.status === 'signedIn') {
    return <Navigate to={afterLoginPath(params.get('next'), state.me)} replace />;
  }

  return (
    <main className={styles.page}>
      <div className={styles.card}>
        <Logo size={86} className={styles.logo} />
        <Suspense fallback={<p role="status">Loading…</p>}>
          <Outlet />
        </Suspense>
      </div>
    </main>
  );
}
