import { Lock } from 'lucide-react';
import styles from './ConfidentialBanner.module.scss';

type ConfidentialBannerProps = {
  crew?: boolean; //crew are told not to take photos, the office not to share content
};

//stays on the page rather than a dialog to click away (T1 7.5)
export function ConfidentialBanner({ crew = false }: ConfidentialBannerProps) {
  return (
    <div className={styles.banner} role="note">
      <Lock className={styles.icon} aria-hidden="true" />
      <p className={styles.text}>
        <strong>Confidential. NDA in effect.</strong>{' '}
        {crew
          ? 'Do not photograph or post content from this event.'
          : 'Content from this event may not be posted externally.'}
      </p>
    </div>
  );
}
