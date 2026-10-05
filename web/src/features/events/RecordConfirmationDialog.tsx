import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { EventDetail, RecordConfirmationRequest } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { useRecordConfirmation } from '@/features/finance/api';
import { parseRand } from '@/lib/format';
import styles from './EventDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//amounts are typed as rand, "84500" or "84 500,50", and sent as numbers
const amount = z
  .string()
  .trim()
  .refine(
    (value) => value === '' || parseRand(value) !== null,
    'Enter an amount in rand, like 12 500 or 12 500,50.',
  );

//the same rules as the api's validator: a po needs its number and date, a deposit its amount,
//date and reference (FR-12)
const confirmationSchema = z
  .object({
    confirmationType: z.enum(['PurchaseOrder', 'Deposit']),
    clientPoNumber: z.string().trim().max(50, 'Keep it under 50 characters.'),
    poReceivedDate: z.string(),
    poAmount: amount,
    depositAmount: amount,
    depositPaidDate: z.string(),
    depositReference: z.string().trim().max(100, 'Keep it under 100 characters.'),
  })
  .superRefine((values, context) => {
    const need = (path: keyof typeof values, message: string) =>
      context.addIssue({ code: 'custom', path: [path], message });
    if (values.confirmationType === 'PurchaseOrder') {
      if (!values.clientPoNumber) need('clientPoNumber', "Enter the client's PO number.");
      if (!values.poReceivedDate) need('poReceivedDate', 'Enter the date the PO was received.');
    } else {
      if (!values.depositAmount) need('depositAmount', 'Enter the deposit amount.');
      if (!values.depositPaidDate) need('depositPaidDate', 'Enter the date the deposit was paid.');
      if (!values.depositReference) need('depositReference', 'Enter the payment reference.');
    }
  });

type ConfirmationValues = z.infer<typeof confirmationSchema>;

//so an api field error lands on the field it's about
const confirmationFields: Record<keyof ConfirmationValues, true> = {
  confirmationType: true,
  clientPoNumber: true,
  poReceivedDate: true,
  poAmount: true,
  depositAmount: true,
  depositPaidDate: true,
  depositReference: true,
};

function requestFrom(values: ConfirmationValues): RecordConfirmationRequest {
  if (values.confirmationType === 'PurchaseOrder') {
    return {
      confirmationType: 'PurchaseOrder',
      clientPoNumber: values.clientPoNumber,
      poReceivedDate: values.poReceivedDate,
      poAmount: values.poAmount ? parseRand(values.poAmount) : null,
    };
  }
  return {
    confirmationType: 'Deposit',
    depositAmount: parseRand(values.depositAmount),
    depositPaidDate: values.depositPaidDate,
    depositReference: values.depositReference,
  };
}

function describeConfirmationError(error: unknown) {
  if (!isApiError(error)) return "Something went wrong, so it wasn't recorded. Try again.";
  if (error.status === 0) return "Couldn't reach Carbonate, so it wasn't recorded. Try again.";
  if (error.status === 403) return "Your role can't record a PO or deposit.";
  return error.problem.detail ?? "Something went wrong, so it wasn't recorded. Try again.";
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type RecordConfirmationDialogProps = {
  event: EventDetail;
  onClose: () => void;
};

//the client's po or deposit, which confirms the booking and puts it on the events board
//TODO(plan): attaching the po or proof of payment waits on event documents (FR-06)
export function RecordConfirmationDialog({ event, onClose }: RecordConfirmationDialogProps) {
  const record = useRecordConfirmation(event.eventId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ConfirmationValues>({
    resolver: zodResolver(confirmationSchema),
    defaultValues: {
      confirmationType: event.paymentMode,
      clientPoNumber: '',
      poReceivedDate: '',
      poAmount: '',
      depositAmount: '',
      depositPaidDate: '',
      depositReference: '',
    },
  });
  const type = useWatch({ control, name: 'confirmationType' });

  async function onSubmit(values: ConfirmationValues) {
    setServerError(null);
    try {
      await record.mutateAsync(requestFrom(values));
      toast.success(`${event.name} is confirmed and on the events board.`);
      onClose();
    } catch (error) {
      const fieldErrors =
        isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      const known = fieldErrors.filter(([key, messages]) => key in confirmationFields && messages[0]);
      known.forEach(([key, messages]) => setError(key as keyof ConfirmationValues, { message: messages[0] }));
      if (known.length === 0) setServerError(describeConfirmationError(error));
    }
  }

  return (
    <Dialog open title="Record PO or deposit" onClose={onClose}>
      <p className={styles.lead}>This confirms the booking, and {event.name} moves onto the events board.</p>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <fieldset className={styles.choices}>
          <legend className={styles.legend}>The client confirmed with</legend>
          <label className={styles.choice}>
            <input type="radio" value="PurchaseOrder" {...register('confirmationType')} />
            Purchase order
          </label>
          <label className={styles.choice}>
            <input type="radio" value="Deposit" {...register('confirmationType')} />
            Deposit
          </label>
        </fieldset>

        {type === 'PurchaseOrder' ? (
          <div className={styles.fields}>
            <Field label="PO number" required error={errors.clientPoNumber?.message}>
              {(field) => (
                <input {...field} {...register('clientPoNumber')} maxLength={50} autoComplete="off" />
              )}
            </Field>
            <Field label="Received on" required error={errors.poReceivedDate?.message}>
              {(field) => <input {...field} {...register('poReceivedDate')} type="date" />}
            </Field>
            <Field
              label="PO amount"
              hint="In rand. Leave it empty if the PO has no amount."
              error={errors.poAmount?.message}
            >
              {(field) => (
                <input {...field} {...register('poAmount')} inputMode="decimal" autoComplete="off" />
              )}
            </Field>
          </div>
        ) : (
          <div className={styles.fields}>
            <Field label="Deposit amount" required hint="In rand." error={errors.depositAmount?.message}>
              {(field) => (
                <input {...field} {...register('depositAmount')} inputMode="decimal" autoComplete="off" />
              )}
            </Field>
            <Field label="Paid on" required error={errors.depositPaidDate?.message}>
              {(field) => <input {...field} {...register('depositPaidDate')} type="date" />}
            </Field>
            <Field label="Payment reference" required error={errors.depositReference?.message}>
              {(field) => (
                <input {...field} {...register('depositReference')} maxLength={100} autoComplete="off" />
              )}
            </Field>
          </div>
        )}

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Record and confirm
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
