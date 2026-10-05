import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { TaskCard } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { formatList } from '@/lib/format';
import { useReturnTask } from './api';
import { describeTaskError } from './rules';
import styles from './TaskDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//the same rules as the api: notes are how the assignee knows what to fix (FR-20)
const returnSchema = z.object({
  reviewNotes: z
    .string()
    .trim()
    .min(1, 'Say what still needs doing before it comes back.')
    .max(2000, 'Keep the notes under 2 000 characters.'),
});

type ReturnValues = z.infer<typeof returnSchema>;

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type ReturnTaskDialogProps = {
  card: TaskCard | null; //open while a card is set
  onClose: () => void;
};

export function ReturnTaskDialog({ card, onClose }: ReturnTaskDialogProps) {
  if (!card) return null;
  return <ReturnForm key={card.cardId} card={card} onClose={onClose} />;
}

function ReturnForm({ card, onClose }: { card: TaskCard; onClose: () => void }) {
  const returnTask = useReturnTask();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ReturnValues>({ resolver: zodResolver(returnSchema), defaultValues: { reviewNotes: '' } });

  async function onSubmit({ reviewNotes }: ReturnValues) {
    setServerError(null);
    try {
      await returnTask.mutateAsync({ card, reviewNotes });
      onClose();
    } catch (error) {
      const fieldError = isApiError(error) && error.status === 400 ? error.problem.errors?.reviewNotes?.[0] : undefined;
      if (fieldError) setError('reviewNotes', { message: fieldError });
      else setServerError(describeTaskError(error, card.subject));
    }
  }

  const assignees = formatList(card.assignees.map((person) => person.fullName));

  return (
    <Dialog open title={`Return ${card.subject}`} onClose={onClose}>
      <p className={styles.lead}>
        It goes back to Assigned{assignees && ` for ${assignees}`}, with your notes at the top.
      </p>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field label="What still needs doing" required error={errors.reviewNotes?.message}>
          {(control) => <textarea {...control} {...register('reviewNotes')} rows={4} maxLength={2000} />}
        </Field>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Return task
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
