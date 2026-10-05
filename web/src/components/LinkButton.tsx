import { Link, type LinkProps } from 'react-router';
import { cx } from '@/lib/cx';
import styles from './Button.module.scss';

type LinkButtonProps = LinkProps & {
  variant?: 'primary' | 'default' | 'ghost';
};

//navigation that looks like a button, it stays a real link so open-in-new-tab still works
export function LinkButton({ variant = 'default', className, ...rest }: LinkButtonProps) {
  return (
    <Link
      className={cx(styles.button, styles.link, variant !== 'default' && styles[variant], className)}
      {...rest}
    />
  );
}
