import { cx } from '@/lib/cx';
import styles from './Logo.module.scss';

type LogoProps = {
  size?: number;
  withName?: boolean;
  className?: string;
};

//the mark is square, so one size drives both width and height and nothing shifts on load.
//it has no wordmark of its own, so withName sets the name beside it as real text, and the
//image goes quiet for screen readers so they don't hear "Carbonate" twice
export function Logo({ size = 60, withName = false, className }: LogoProps) {
  if (!withName) {
    return <img src="/carbonate-logo.svg" alt="Carbonate" width={size} height={size} className={className} />;
  }

  return (
    <span className={cx(styles.lockup, className)}>
      <img src="/carbonate-logo.svg" alt="" width={size} height={size} className={styles.mark} />
      <span className={styles.name}>Carbonate</span>
    </span>
  );
}
