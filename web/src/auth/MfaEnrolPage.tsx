import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, useSearchParams } from 'react-router';
import { apiFetch } from '@/api/client';
import type { MfaEnrolResponse } from '@/api/types';
import { Alert } from '@/components/Alert';
import { useAuth } from './AuthContext';
import { MfaCodeForm } from './MfaCodeForm';
import { nextQuery } from './redirect';
import styles from './Auth.module.scss';

//----------------------------------------------------------\\
//                              SETUP KEY
//----------------------------------------------------------\\

//pulls the key out of the otpauth:// uri, and only trusts that one scheme for the app link
function readSetupKey(uri: string) {
  try {
    const url = new URL(uri);
    const secret = url.searchParams.get('secret');
    if (url.protocol !== 'otpauth:' || !secret) return null;
    return { uri, grouped: secret.match(/.{1,4}/g)?.join(' ') ?? secret };
  } catch {
    return null;
  }
}

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//first login for director and accounts (FR-34)
//TODO(plan): add a qr code once the team agrees on a small library, until then the key is typed in
export default function MfaEnrolPage() {
  const { pendingMfa } = useAuth();
  const [params] = useSearchParams();
  const backToLogin = `/login${nextQuery(params.get('next'))}`;
  const mfaToken = pendingMfa?.mfaToken;

  //every enrol call makes a new secret, so this must never refetch behind the user's back,
  //especially when they switch to their authenticator app and come back
  const enrolment = useQuery({
    queryKey: ['mfa-enrolment', mfaToken],
    queryFn: () => apiFetch<MfaEnrolResponse>('/auth/mfa/enrol', { method: 'POST', json: { mfaToken } }),
    enabled: mfaToken !== undefined,
    staleTime: Infinity,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  if (!mfaToken) return <Navigate to={backToLogin} replace />;

  const key = enrolment.data ? readSetupKey(enrolment.data.otpauthUri) : null;
  const failed = enrolment.isError || (enrolment.isSuccess && !key);

  return (
    <>
      <title>Set up two-step sign-in — Carbonate</title>
      <h1 className={styles.heading}>Set up two-step sign-in</h1>
      <p className={styles.lead}>
        Your role needs a code from your phone each time you log in. Setup takes a minute.
      </p>

      {enrolment.isPending && <p role="status">Getting your setup key…</p>}

      {failed && (
        <Alert tone="danger">
          We couldn't start the setup. <Link to={backToLogin}>Start again</Link>
        </Alert>
      )}

      {key && (
        <>
          <ol className={styles.steps}>
            <li>
              Open an authenticator app on your phone, such as Google Authenticator or Microsoft
              Authenticator, and add an account.
            </li>
            <li>
              Choose to enter a setup key, and type this one:
              <code className={styles.secret}>{key.grouped}</code>
              <a href={key.uri}>On this phone? Open it in your authenticator app</a>
            </li>
            <li>Enter the 6-digit code the app shows.</li>
          </ol>
          <MfaCodeForm endpoint="/auth/mfa/confirm" mfaToken={mfaToken} submitLabel="Finish setup" />
        </>
      )}
    </>
  );
}
