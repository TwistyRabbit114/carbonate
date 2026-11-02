import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { useChangePassword } from './api';
import styles from './AccountPage.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//the api's rules for a new password, the same as a new account's first one. it also turns away
//passwords from known breaches, which only it can check
const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: z.string().min(12, 'Use at least 12 characters.').max(128, 'Keep it under 128 characters.'),
    repeatPassword: z.string().min(1, 'Type the new password again.'),
  })
  .refine((values) => values.repeatPassword === values.newPassword, {
    path: ['repeatPassword'],
    message: "That doesn't match the new password.",
  });

type ChangePasswordValues = z.infer<typeof changePasswordSchema>;

const emptyForm: ChangePasswordValues = { currentPassword: '', newPassword: '', repeatPassword: '' };

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

function describeError(error: unknown) {
  if (isApiError(error) && error.status === 429) return 'Too many attempts. Wait a minute and try again.';
  //what the server answers until the endpoint is on it
  if (isApiError(error) && error.status === 404) return "Changing your password isn't switched on yet.";
  return problemMessage(error, "Your password wasn't changed.");
}

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//the repeat box only lives here, the api gets the current and new passwords
export function ChangePasswordForm() {
  const change = useChangePassword();
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: emptyForm,
  });

  async function onSubmit({ currentPassword, newPassword }: ChangePasswordValues) {
    setServerError(null);
    try {
      await change.mutateAsync({ currentPassword, newPassword });
      reset(emptyForm);
      toast.success('Password changed. Use the new one next time you log in.');
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        key === 'currentPassword' || key === 'newPassword' ? key : undefined,
      );
      if (!landed) setServerError(describeError(error));
    }
  }

  return (
    <form className={styles.passwordForm} onSubmit={handleSubmit(onSubmit)} noValidate>
      {serverError && <Alert tone="danger">{serverError}</Alert>}
      <Field label="Current password" required error={errors.currentPassword?.message}>
        {(field) => (
          <input
            {...field}
            {...register('currentPassword')}
            type="password"
            autoComplete="current-password"
          />
        )}
      </Field>
      <Field
        label="New password"
        required
        hint="At least 12 characters. Carbonate refuses passwords that have been in a data breach."
        error={errors.newPassword?.message}
      >
        {(field) => (
          <input {...field} {...register('newPassword')} type="password" autoComplete="new-password" />
        )}
      </Field>
      <Field label="Confirm new password" required error={errors.repeatPassword?.message}>
        {(field) => (
          <input {...field} {...register('repeatPassword')} type="password" autoComplete="new-password" />
        )}
      </Field>
      <div>
        <Button type="submit" variant="primary" busy={isSubmitting}>
          Change password
        </Button>
      </div>
    </form>
  );
}
