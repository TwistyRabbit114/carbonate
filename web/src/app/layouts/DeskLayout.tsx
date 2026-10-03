import { Suspense, useId, useRef } from 'react';
import { Ellipsis } from 'lucide-react';
import { NavLink, Outlet } from 'react-router';
import { usePermissions } from '@/auth/AuthContext';
import { Logo } from '@/components/Logo';
import { PageSkeleton } from '@/components/PageSkeleton';
import { cx } from '@/lib/cx';
import { deskNav, visibleNav, type NavItem } from '../navigation';
import { UserBlock } from './UserBlock';
import styles from './DeskLayout.module.scss';

//----------------------------------------------------------\\
//                              LAYOUT
//----------------------------------------------------------\\

//the phone bottom bar fits five items, so a sixth pushes the fifth onwards into "More"
const phoneBarLimit = 5;

//sidebar on desktop and tablet, bottom tab bar on phones (760px and under)
export function DeskLayout() {
  const items = visibleNav(deskNav, usePermissions());
  const overflow = items.length > phoneBarLimit ? items.slice(phoneBarLimit - 1) : [];

  return (
    <div className={styles.shell}>
      <a href="#main" className="skip-link">
        Skip to content
      </a>

      <header className={styles.sidebar}>
        <div className={styles.brand}>
          <Logo size={60} />
        </div>

        <nav aria-label="Main" className={styles.nav}>
          <ul className={cx(styles.navList, overflow.length > 0 && styles.overflowing)}>
            {items.map((item) => (
              <li key={item.to}>
                <DeskNavLink item={item} />
              </li>
            ))}
            {overflow.length > 0 && <MoreMenu items={overflow} />}
          </ul>
        </nav>

        <div className={styles.userArea}>
          <UserBlock />
        </div>
      </header>

      <main id="main" className={styles.content} tabIndex={-1}>
        <Suspense fallback={<PageSkeleton />}>
          <Outlet />
        </Suspense>
      </main>
    </div>
  );
}

//----------------------------------------------------------\\
//                              PIECES
//----------------------------------------------------------\\

function DeskNavLink({ item, onNavigate }: { item: NavItem; onNavigate?: () => void }) {
  const Icon = item.icon;
  return (
    <NavLink to={item.to} className={styles.navLink} onClick={onNavigate}>
      <Icon className={styles.navIcon} aria-hidden="true" />
      <span>{item.label}</span>
    </NavLink>
  );
}

//native popover, so esc and tapping outside close it without any extra code
function MoreMenu({ items }: { items: readonly NavItem[] }) {
  const id = useId();
  const panel = useRef<HTMLDivElement>(null);

  return (
    <li className={styles.more}>
      <button type="button" className={styles.navLink} popoverTarget={id}>
        <Ellipsis className={styles.navIcon} aria-hidden="true" />
        <span>More</span>
      </button>
      <div id={id} popover="auto" ref={panel} className={styles.morePanel}>
        <ul className={styles.moreList}>
          {items.map((item) => (
            <li key={item.to}>
              <DeskNavLink item={item} onNavigate={() => panel.current?.hidePopover?.()} />
            </li>
          ))}
        </ul>
      </div>
    </li>
  );
}
