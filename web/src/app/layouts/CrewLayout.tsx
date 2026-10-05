import { Suspense } from 'react';
import { NavLink, Outlet } from 'react-router';
import { usePermissions } from '@/auth/AuthContext';
import { Logo } from '@/components/Logo';
import { PageSkeleton } from '@/components/PageSkeleton';
import { crewNav, visibleNav } from '../navigation';
import { UserBlock } from './UserBlock';
import styles from './CrewLayout.module.scss';

//phone-first for crew at venues: bottom tabs at every width, one centred column (NFR-23)
export function CrewLayout() {
  const tabs = visibleNav(crewNav, usePermissions());

  return (
    <div className={styles.shell}>
      <a href="#main" className="skip-link">
        Skip to content
      </a>

      <header className={styles.topBar}>
        <Logo size={40} withName />
        <UserBlock />
      </header>

      <main id="main" className={styles.content} tabIndex={-1}>
        <Suspense fallback={<PageSkeleton />}>
          <Outlet />
        </Suspense>
      </main>

      <nav aria-label="Main" className={styles.tabBar}>
        <ul className={styles.tabList}>
          {tabs.map((tab) => {
            const Icon = tab.icon;
            return (
              <li key={tab.to}>
                <NavLink to={tab.to} className={styles.tab}>
                  <Icon className={styles.tabIcon} aria-hidden="true" />
                  <span>{tab.label}</span>
                </NavLink>
              </li>
            );
          })}
        </ul>
      </nav>
    </div>
  );
}
