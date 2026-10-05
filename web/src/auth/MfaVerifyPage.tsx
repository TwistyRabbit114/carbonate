import { Link, Navigate, useSearchParams } from 'react-router';
import { useAuth } from './AuthContext';
import { MfaCodeForm } from './MfaCodeForm';
import { nextQuery } from './redirect';
import styles from './Auth.module.scss';

export default function MfaVerifyPage() {
  const { pendingMfa } = useAuth();
  const [params] = useSearchParams();
  const backToLogin = `/login${nextQuery(params.get('next'))}`;

  //the mfa token only lives in memory, so after a reload there's nothing to verify
  if (!pendingMfa) return <Navigate to={backToLogin} replace />;

  return (
    <>
      <title>Enter your code · Carbonate</title>
      <h1 className={styles.heading}>Enter your code</h1>
      <p className={styles.lead}>Open your authenticator app and enter the 6-digit code for Carbonate.</p>
      <MfaCodeForm step="verify" mfaToken={pendingMfa.mfaToken} submitLabel="Verify" />
      <p className={styles.footnote}>
        <Link to={backToLogin}>Start again</Link>
      </p>
    </>
  );
}
