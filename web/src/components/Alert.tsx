import type { ReactNode } from 'react';
import { CircleAlert, Info, TriangleAlert } from 'lucide-react';
import { cx } from '@/lib/cx';
import styles from './Alert.module.scss';

type AlertProps = {
  tone?: 'danger' | 'warning' | 'info';
  children: ReactNode;
};

const icons = { danger: CircleAlert, warning: TriangleAlert, info: Info };

//colour is never the only signal, so every alert carries an icon and words
export function Alert({ tone = 'info', children }: AlertProps) {
  const Icon = icons[tone];

  return (
    <div role={tone === 'danger' ? 'alert' : 'status'} className={cx(styles.alert, styles[tone])}>
      <Icon className={styles.icon} aria-hidden="true" />
      <div>{children}</div>
    </div>
  );
}
