import type { ReactNode } from 'react';
import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router';
import styles from './BackLink.module.scss';

type BackLinkProps = {
  to: string;
  children: ReactNode;
};

//the "← Admin Tasks" in a page's eyebrow, back to where this record lives
export function BackLink({ to, children }: BackLinkProps) {
  return (
    <Link to={to} className={styles.back}>
      <ArrowLeft className={styles.icon} aria-hidden="true" />
      {children}
    </Link>
  );
}
