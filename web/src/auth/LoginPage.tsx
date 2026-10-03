import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useNavigate, useSearchParams } from 'react-router';
import { z } from 'zod';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import type { LoginResponse } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { useAuth } from './AuthContext';
import { nextQuery } from './redirect';
import styles from './Auth.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//no length rules here, the 12 character minimum applies when a password is set, not when it's typed
const loginSchema = z.object({
  email: z.string().trim().min(1, 'Enter your email address').pipe(z.email('Enter a valid email address')),
  password: z.string().min(1, 'Enter your password'),
});

type LoginValues = z.infer<typeof loginSchema>;

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

//wrong email and wrong password read the same, so the page never confirms an account exists
function describeLoginError(error: unknown) {
  if (!isApiError(error)) return 'Something went wrong. Try again.';
  if (error.status === 0) return "Can't reach Carbonate right now. Check your connection and try again.";

  //TODO(plan): how do lockout and a deactivated account come back from login? assumed 423 or a problem type, confirm with C
  if (error.status === 423 || error.problem.type?.endsWith('/account-locked')) {
    return 'Too many attempts. Try again in 15 minutes.';
  }
  if (error.status === 429) return 'Too many attempts. Wait a few minutes and try again.';
  if (error.status === 400 || error.status === 401) return 'Email or password is incorrect.';
  return 'Something went wrong. Try again.';
}

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

export default function LoginPage() {
  const { beginMfa, completeLogin } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  async function onSubmit(values: LoginValues) {
    setServerError(null);
    try {
      const result = await apiFetch<LoginResponse>('/auth/login', { method: 'POST', json: values });

      //director and accounts need a code first (FR-34), the token for that step stays in memory
      if ('mfaRequired' in result) {
        beginMfa({ mfaToken: result.mfaToken });
        const step = result.mfaEnrolmentRequired ? 'mfa-setup' : 'mfa';
        navigate(`/login/${step}${nextQuery(params.get('next'))}`);
        return;
      }

      //the layout moves on as soon as the session exists
      await completeLogin(result.accessToken);
    } catch (error) {
      setServerError(describeLoginError(error));
    }
  }

  const expired = params.get('reason') === 'expired';

  return (
    <>
      <title>Log in — Carbonate</title>
      <h1 className={styles.heading}>Log in</h1>
      <p className={styles.lead}>Carbon Events coordination</p>

      <div className={styles.alerts}>
        {serverError && <Alert tone="danger">{serverError}</Alert>}
        {expired && !serverError && (
          <Alert tone="info">Your session has ended. Log in again to carry on.</Alert>
        )}
      </div>

      <form className={styles.form} onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field label="Email" error={errors.email?.message}>
          {(control) => <input {...control} {...register('email')} type="email" autoComplete="username" />}
        </Field>
        <Field label="Password" error={errors.password?.message}>
          {(control) => (
            <input {...control} {...register('password')} type="password" autoComplete="current-password" />
          )}
        </Field>
        <Button type="submit" variant="primary" block busy={isSubmitting} className={styles.submit}>
          Log in
        </Button>
      </form>
    </>
  );
}
