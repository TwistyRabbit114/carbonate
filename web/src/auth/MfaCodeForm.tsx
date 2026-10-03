import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import type { TokenResponse } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { useAuth } from './AuthContext';
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

function describeCodeError(error: unknown) {
  if (isApiError(error) && error.status === 0) {
    return "Can't reach Carbonate right now. Check your connection and try again.";
  }
  if (isApiError(error) && (error.status === 400 || error.status === 401)) {
    return "That code didn't work. Codes change every 30 seconds, so try the one showing now.";
  }
  return 'Something went wrong. Try again.';
}

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

type MfaCodeFormProps = {
  endpoint: '/auth/mfa/verify' | '/auth/mfa/confirm';
  mfaToken: string;
  submitLabel: string;
};

//shared by verify (every login) and confirm (first-time setup), both trade a code for a session
export function MfaCodeForm({ endpoint, mfaToken, submitLabel }: MfaCodeFormProps) {
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
      const { accessToken } = await apiFetch<TokenResponse>(endpoint, {
        method: 'POST',
        json: { mfaToken, code },
      });
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
