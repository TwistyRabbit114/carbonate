import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { UninvoicedEvent } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { formatDateOnly, parseRand, sastCalendarDate } from '@/lib/format';
import { useRaiseInvoice } from './api';
import form from '@/components/FormLayout.module.scss';

const raiseSchema = z.object({
  issuedDate: z.string().min(1, 'Enter the date it was sent.'),
  amount: z
    .string()
    .trim()
    .refine((value) => value === '' || parseRand(value) !== null, 'Enter the amount in rand, like 61 525.'),
});

type RaiseValues = z.infer<typeof raiseSchema>;

type RaiseInvoiceDialogProps = {
  event: UninvoicedEvent & { confirmationId: string };
  onClose: () => void;
};

//an invoice against the event's po or deposit (FR-13). left blank, the amount is the accepted
//costing's total, so nobody retypes it from memory. the due date follows the client's terms
export function RaiseInvoiceDialog({ event, onClose }: RaiseInvoiceDialogProps) {
  const raise = useRaiseInvoice(event.eventId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RaiseValues>({
    resolver: zodResolver(raiseSchema),
    defaultValues: { issuedDate: sastCalendarDate(new Date().toISOString()), amount: '' },
  });

  async function onSubmit({ issuedDate, amount }: RaiseValues) {
    setServerError(null);
    try {
      const invoice = await raise.mutateAsync({
        confirmationId: event.confirmationId,
        issuedDate,
        amountIncVat: amount ? parseRand(amount) : null,
      });
      toast.success(`${invoice.invoiceNumber} is raised, due ${formatDateOnly(invoice.dueDate)}.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        key === 'issuedDate' ? key : key === 'amountIncVat' ? 'amount' : undefined,
      );
      if (!landed) setServerError(problemMessage(error, 'No invoice was raised.'));
    }
  }

  return (
    <Dialog open title={`Raise an invoice for ${event.eventCode}`} onClose={onClose}>
      <p className={form.lead}>
        {event.name} for {event.clientName}, held on {formatDateOnly(event.eventDate)}. It goes against{' '}
        {event.confirmationReference}.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Sent on" required error={errors.issuedDate?.message}>
            {(field) => <input {...field} {...register('issuedDate')} type="date" />}
          </Field>
          <Field
            label="Amount incl. VAT"
            hint="Leave blank to use the accepted costing's total."
            error={errors.amount?.message}
          >
            {(field) => <input {...field} {...register('amount')} inputMode="decimal" autoComplete="off" />}
          </Field>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Raise invoice
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
