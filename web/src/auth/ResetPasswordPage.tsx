import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link, useLocation } from 'react-router';
import { z } from 'zod';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import type { ResetPasswordRequest } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { LinkButton } from '@/components/LinkButton';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import styles from './Auth.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//the same rules as changing it from the account page. only the api can check for breaches
const resetSchema = z
  .object({
    newPassword: z.string().min(12, 'Use at least 12 characters.').max(128, 'Keep it under 128 characters.'),
    repeatPassword: z.string().min(1, 'Type the new password again.'),
  })
  .refine((values) => values.repeatPassword === values.newPassword, {
    path: ['repeatPassword'],
    message: "That doesn't match the new password.",
  });

type ResetValues = z.infer<typeof resetSchema>;

//the link an administrator hands over ends in #token=..., kept out of the address the server sees
const tokenFrom = (hash: string) => new URLSearchParams(hash.replace(/^#/, '')).get('token');

function describeError(error: unknown) {
  if (isApiError(error) && error.status === 429) return 'Too many attempts. Wait a minute and try again.';
  //a used, expired or mistyped link all read the same
  const tokenProblem = isApiError(error) ? error.problem.errors?.token?.[0] : undefined;
  return tokenProblem ?? problemMessage(error, "Your password wasn't changed.");
}

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//someone who lost their password, with the single-use link from the users panel (FR-38). it ends
//every session they had, so they log in fresh afterwards
export default function ResetPasswordPage() {
  const { hash } = useLocation();
  const [token] = useState(() => tokenFrom(hash));
  const [done, setDone] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetValues>({ resolver: zodResolver(resetSchema) });

  async function onSubmit({ newPassword }: ResetValues) {
    if (!token) return;
    setServerError(null);
    try {
      const reset: ResetPasswordRequest = { token, newPassword };
      await apiFetch<void>('/auth/password/reset', { method: 'POST', json: reset });
      setDone(true);
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => (key === 'newPassword' ? key : undefined));
      if (!landed) setServerError(describeError(error));
    }
  }

  if (done) {
    return (
      <>
        <title>Password changed · Carbonate</title>
        <h1 className={styles.heading}>Password changed</h1>
        <p className={styles.lead}>
          Log in with your new password. Any device you were signed in on has been signed out.
        </p>
        <LinkButton to="/login" variant="primary" className={styles.submit}>
          Log in
        </LinkButton>
      </>
    );
  }

  return (
    <>
      <title>Choose a new password · Carbonate</title>
      <h1 className={styles.heading}>Choose a new password</h1>
      <p className={styles.lead}>The link works once, and only for a short while after it was made.</p>

      <div className={styles.alerts}>
        {!token && (
          <Alert tone="danger">
            This page needs the reset link your Director or Operations Manager gave you. Open the whole link,
            or ask them for a new one.
          </Alert>
        )}
        {serverError && <Alert tone="danger">{serverError}</Alert>}
      </div>

      {token && (
        <form className={styles.form} onSubmit={handleSubmit(onSubmit)} noValidate>
          <Field
            label="New password"
            hint="At least 12 characters. Carbonate refuses passwords that have been in a data breach."
            error={errors.newPassword?.message}
          >
            {(control) => (
              <input {...control} {...register('newPassword')} type="password" autoComplete="new-password" />
            )}
          </Field>
          <Field label="Confirm new password" error={errors.repeatPassword?.message}>
            {(control) => (
              <input
                {...control}
                {...register('repeatPassword')}
                type="password"
                autoComplete="new-password"
              />
            )}
          </Field>
          <Button type="submit" variant="primary" block busy={isSubmitting} className={styles.submit}>
            Save new password
          </Button>
        </form>
      )}

      <p className={styles.footnote}>
        <Link to="/login">Back to log in</Link>
      </p>
    </>
  );
}
