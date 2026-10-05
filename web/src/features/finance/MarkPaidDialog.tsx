import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { Invoice } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { formatDateOnly, sastCalendarDate } from '@/lib/format';
import { useUpdateInvoice } from './api';
import form from '@/components/FormLayout.module.scss';

type MarkPaidDialogProps = {
  invoice: Invoice;
  onClose: () => void;
};

//the money is in (invoice.manage). it can't be paid before it was sent, the api checks that too
export function MarkPaidDialog({ invoice, onClose }: MarkPaidDialogProps) {
  const update = useUpdateInvoice(invoice.invoiceId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const schema = z.object({
    paidDate: z
      .string()
      .min(1, 'Enter the date it was paid.')
      .refine((value) => value >= invoice.issuedDate, 'It cannot be paid before it was issued.'),
  });
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<{ paidDate: string }>({
    resolver: zodResolver(schema),
    defaultValues: { paidDate: sastCalendarDate(new Date().toISOString()) },
  });

  async function onSubmit({ paidDate }: { paidDate: string }) {
    setServerError(null);
    try {
      await update.mutateAsync({ status: 'Paid', paidDate });
      toast.success(`${invoice.invoiceNumber} is marked paid.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => (key === 'paidDate' ? key : undefined));
      if (!landed) setServerError(problemMessage(error, 'It is still marked as sent.'));
    }
  }

  return (
    <Dialog open title={`Mark ${invoice.invoiceNumber} paid`} onClose={onClose}>
      <p className={form.lead}>
        For {invoice.eventCode}, sent on {formatDateOnly(invoice.issuedDate)} against{' '}
        {invoice.confirmationReference}.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field label="Paid on" required error={errors.paidDate?.message}>
          {(field) => <input {...field} {...register('paidDate')} type="date" />}
        </Field>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Mark paid
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
