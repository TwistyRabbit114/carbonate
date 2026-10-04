import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import type { SignedInSession } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { mfaBearer, useAuth } from './AuthContext';
import styles from './Auth.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//apps show the code as "123 456", so spaces are dropped before checking
const codeSchema = z.object({
  code: z
    .string()
    .transform((value) => value.replace(/\s/g, ''))
    .pipe(z.string().regex(/^\d{6}$/, 'Enter the 6-digit code from your authenticator app')),
});

type CodeInput = z.input<typeof codeSchema>;
type CodeValues = z.output<typeof codeSchema>;

//a wrong code and an mfa token that ran out (after a few minutes) both come back as 401,
//so the message covers both
//TODO(plan): could the api give an expired sign-in its own problem type, so this can say which? ask C
function describeCodeError(error: unknown) {
  if (!isApiError(error)) return 'Something went wrong. Try again.';
  if (error.status === 0) return "Can't reach Carbonate right now. Check your connection and try again.";
  if (error.status === 429) return 'Too many attempts. Wait a minute and try again.';
  if (error.status === 400 || error.status === 401) {
    return "That code didn't work. Try the one showing in your app now, or start again if this page has been open a few minutes.";
  }
  return 'Something went wrong. Try again.';
}

//----------------------------------------------------------\\
//                              REQUEST
//----------------------------------------------------------\\

type MfaStepName = 'verify' | 'confirm';

//verify sends the mfa token in the body, confirm sends it as the bearer token
function sendCode(step: MfaStepName, mfaToken: string, code: string) {
  return step === 'verify'
    ? apiFetch<SignedInSession>('/auth/mfa/verify', { method: 'POST', json: { mfaToken, code } })
    : apiFetch<SignedInSession>('/auth/mfa/confirm', {
        method: 'POST',
        headers: mfaBearer(mfaToken),
        json: { code },
      });
}

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

type MfaCodeFormProps = {
  step: MfaStepName;
  mfaToken: string;
  submitLabel: string;
};

//shared by verify (every login) and confirm (first-time setup), both trade a code for a session
export function MfaCodeForm({ step, mfaToken, submitLabel }: MfaCodeFormProps) {
  const { completeLogin } = useAuth();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CodeInput, unknown, CodeValues>({ resolver: zodResolver(codeSchema) });

  async function onSubmit({ code }: CodeValues) {
    setServerError(null);
    try {
      const { accessToken } = await sendCode(step, mfaToken, code);
      await completeLogin(accessToken);
    } catch (error) {
      setServerError(describeCodeError(error));
    }
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit(onSubmit)} noValidate>
      {serverError && <Alert tone="danger">{serverError}</Alert>}
      <Field label="6-digit code" error={errors.code?.message}>
        {(control) => (
          <input
            {...control}
            {...register('code')}
            className={styles.code}
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={7}
          />
        )}
      </Field>
      <Button type="submit" variant="primary" block busy={isSubmitting} className={styles.submit}>
        {submitLabel}
      </Button>
    </form>
  );
}
