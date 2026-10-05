import type { ReactNode } from 'react';
import { cx } from '@/lib/cx';
import styles from './Badge.module.scss';

type BadgeProps = {
  tone?: 'neutral' | 'confidential' | 'warning' | 'auto' | 'danger';
  children: ReactNode;
};

//always words, never colour alone: "Confidential", "Auto", "High"
export function Badge({ tone = 'neutral', children }: BadgeProps) {
  return <span className={cx(styles.badge, styles[tone])}>{children}</span>;
}
