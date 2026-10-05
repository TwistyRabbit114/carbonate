import { CloudAlert } from 'lucide-react';
import { Button } from './Button';
import styles from './EmptyState.module.scss';

type ErrorStateProps = {
  message?: string;
  onRetry?: () => void;
};

//no codes or stack traces, just what happened and a way to try again
export function ErrorState({ message = 'Something went wrong loading this.', onRetry }: ErrorStateProps) {
  return (
    <div className={styles.empty} role="alert">
      <CloudAlert className={styles.icon} aria-hidden="true" />
      <h2 className={styles.title}>{message}</h2>
      {onRetry && (
        <Button variant="primary" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}
