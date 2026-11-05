import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { EventDetail } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, isConflict, problemMessage } from '@/lib/apiErrors';
import { formatCount } from '@/lib/format';
import { useRecordPackSize } from './api';
import styles from './EventDialogs.module.scss';

const packSizeSchema = z.object({
  actual: z
    .string()
    .trim()
    .regex(/^\d+$/, 'Enter the number of guests, like 180.')
    .refine((value) => Number(value) <= 100_000, 'Keep it under 100 000.'),
});

type PackSizeValues = z.infer<typeof packSizeSchema>;

type PackSizeDialogProps = {
  event: EventDetail;
  onClose: () => void;
};

//the confirmed number of guests (FR-08). the estimate stays as it was booked, and is changed on
//the edit page
export function PackSizeDialog({ event, onClose }: PackSizeDialogProps) {
  const record = useRecordPackSize(event.eventId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PackSizeValues>({
    resolver: zodResolver(packSizeSchema),
    defaultValues: { actual: event.packSizeActual != null ? String(event.packSizeActual) : '' },
  });

  async function onSubmit({ actual }: PackSizeValues) {
    setServerError(null);
    try {
      await record.mutateAsync({ packSizeActual: Number(actual), rowVersion: event.rowVersion });
      toast.success(`${event.name} is down for ${formatCount(Number(actual))} guests.`);
      onClose();
    } catch (error) {
      if (isConflict(error)) {
        setServerError(
          'Someone else changed this event while you were looking at it. Close this and try again.',
        );
        return;
      }
      const landed = applyFieldErrors(error, setError, (key) =>
        key === 'packSizeActual' ? 'actual' : undefined,
      );
      if (!landed) setServerError(problemMessage(error, 'The pack size was not changed.'));
    }
  }

  return (
    <Dialog open title="Actual pack size" onClose={onClose}>
      <p className={styles.lead}>
        Booked as {formatCount(event.packSizeEstimated)} guests. Enter how many are confirmed.
      </p>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field label="Confirmed guests" required error={errors.actual?.message}>
          {(field) => <input {...field} {...register('actual')} inputMode="numeric" autoComplete="off" />}
        </Field>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Save
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
