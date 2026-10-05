import { useState } from 'react';
import { LogOut } from 'lucide-react';
import { useAuth, useSignedIn } from '@/auth/AuthContext';
import { describeRoles, initials } from '@/auth/roles';
import styles from './UserBlock.module.scss';

//avatar, name, role and logout. the name and role drop away on phones to save room
export function UserBlock() {
  const me = useSignedIn();
  const { logout } = useAuth();
  const [leaving, setLeaving] = useState(false);

  async function handleLogout() {
    setLeaving(true);
    await logout();
  }

  return (
    <div className={styles.user}>
      <span className={styles.avatar} aria-hidden="true">
        {initials(me.user.fullName)}
      </span>
      <div className={styles.text}>
        <div className={styles.name}>{me.user.fullName}</div>
        <div className={styles.role}>{describeRoles(me.roles)}</div>
      </div>
      <button
        type="button"
        className={styles.logout}
        onClick={handleLogout}
        disabled={leaving}
        aria-label="Log out"
        title="Log out"
      >
        <LogOut aria-hidden="true" />
      </button>
    </div>
  );
}
