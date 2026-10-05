import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { permissionCheck } from '@/auth/permissions';
import { Logo } from '@/components/Logo';
import { landingPath } from '../navigation';
import styles from './Centered.module.scss';

//worded so it never confirms that something exists but isn't shared with this user
export function NotFoundPage() {
  const { state } = useAuth();
  const home = state.status === 'signedIn' ? landingPath(permissionCheck(state.me.permissions)) : '/login';

  return (
    <main className={styles.page}>
      <title>Not found — Carbonate</title>
      <div className={styles.card}>
        <Logo size={48} withName className={styles.logo} />
        <h1>We couldn't find that</h1>
        <p>It may have been removed, or it isn't shared with you.</p>
        <Link to={home}>Go to your start page</Link>
      </div>
    </main>
  );
}
