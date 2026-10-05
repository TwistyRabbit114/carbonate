import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';
import { z } from 'zod';
import type { GenerateOrderListsResponse } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { sastCalendarDate } from '@/lib/format';
import { useGenerateOrderLists } from './api';
import form from '@/components/FormLayout.module.scss';
import styles from './StockPage.module.scss';

const periodSchema = z
  .object({
    from: z.string().min(1, 'Choose the start of the period.'),
    to: z.string().min(1, 'Choose the end of the period.'),
  })
  .refine((values) => !values.from || !values.to || values.to >= values.from, {
    path: ['to'],
    message: 'The end of the period cannot be before the start.',
  });

type PeriodValues = z.infer<typeof periodSchema>;

const day = 24 * 3_600_000;

//the next fortnight, worked out once when the dialog opens
function nextFortnight(): PeriodValues {
  const now = Date.now();
  return {
    from: sastCalendarDate(new Date(now).toISOString()),
    to: sastCalendarDate(new Date(now + 14 * day).toISOString()),
  };
}

//one list per supplier for everything to order or rent across the confirmed and live events in
//the period (FR-28). items with no supplier come back as a warning rather than a list
export function GenerateOrderListsDialog({ onClose }: { onClose: () => void }) {
  const generate = useGenerateOrderLists();
  const [result, setResult] = useState<GenerateOrderListsResponse | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [period] = useState(nextFortnight);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PeriodValues>({
    resolver: zodResolver(periodSchema),
    defaultValues: period,
  });

  async function onSubmit(period: PeriodValues) {
    setServerError(null);
    try {
      setResult(await generate.mutateAsync(period));
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        key === 'from' || key === 'to' ? key : undefined,
      );
      if (!landed) setServerError(problemMessage(error, 'No lists were made.'));
    }
  }

  if (result) {
    return (
      <Dialog open title="Order lists made" onClose={onClose}>
        {result.orderLists.length === 0 ? (
          <p className={form.lead}>There's nothing to order or rent for confirmed events in that period.</p>
        ) : (
          <>
            <p className={form.lead}>
              {result.orderLists.length === 1
                ? 'One list, as a draft:'
                : `${result.orderLists.length} lists, as drafts:`}
            </p>
            <ul className={styles.madeLists}>
              {result.orderLists.map((list) => (
                <li key={list.orderListId}>
                  <Link to={`/stock/order-lists/${list.orderListId}`}>{list.supplierName}</Link>,{' '}
                  {list.lines.length === 1 ? '1 line' : `${list.lines.length} lines`}
                </li>
              ))}
            </ul>
          </>
        )}
        {result.unassignedSupplier.length > 0 && (
          <div className={form.alert}>
            <Alert tone="warning">
              These have no supplier, so they aren't on any list:{' '}
              {result.unassignedSupplier.map((item) => item.stockItemName).join(', ')}. Give them a supplier
              in the catalogue, then make the lists again.
            </Alert>
          </div>
        )}
        <DialogFooter>
          <Button variant="primary" onClick={onClose}>
            Done
          </Button>
        </DialogFooter>
      </Dialog>
    );
  }

  return (
    <Dialog open title="Generate order lists" onClose={onClose}>
      <p className={form.lead}>
        Everything to order or rent for confirmed events needed in this period, one list per supplier.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Needed from" required error={errors.from?.message}>
            {(field) => <input {...field} {...register('from')} type="date" />}
          </Field>
          <Field label="Needed until" required error={errors.to?.message}>
            {(field) => <input {...field} {...register('to')} type="date" />}
          </Field>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Generate
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
