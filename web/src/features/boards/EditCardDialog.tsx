import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import type { QueryKey } from '@tanstack/react-query';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { Milestone, TaskCard } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { DescriptionEditor, formattingHint } from '@/components/DescriptionEditor';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { milestoneLabels } from '@/features/events/labels';
import { formatDateTime, fromSastDateTimeInput, sastCalendarDate, sastDateTimeInput } from '@/lib/format';
import { htmlToMarkup, markupToHtml } from '@/lib/richText';
import {
  cardPriorities,
  currentCardIn,
  describeCardError,
  endOfWorkingDay,
  useUpdateCard,
  type CardFields,
} from './cards';
import styles from './CardDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const priorities = cardPriorities;

const editSchema = z.object({
  subject: z
    .string()
    .trim()
    .min(1, 'Give the card a subject.')
    .max(200, 'Keep the subject under 200 characters.'),
  description: z.string().max(5000, 'Keep the description under 5 000 characters.'),
  priority: z.enum(priorities),
  due: z.string(), //a date for admin tasks, a date and time for event cards, or empty
  milestoneId: z.string(),
});

type EditValues = z.infer<typeof editSchema>;

const formFieldFor: Record<string, keyof EditValues> = {
  subject: 'subject',
  description: 'description',
  priority: 'priority',
  dueAt: 'due',
  milestoneId: 'milestoneId',
};

//admin tasks are due on a day, event cards at a time (often a few hours before a milestone)
export type DueKind = 'day' | 'time';

function dueInput(dueAt: string | null, kind: DueKind) {
  if (!dueAt) return '';
  return kind === 'day' ? sastCalendarDate(dueAt) : sastDateTimeInput(dueAt);
}

function valuesFrom(card: TaskCard, due: DueKind): EditValues {
  return {
    subject: card.subject,
    description: htmlToMarkup(card.description),
    priority: card.priority,
    due: dueInput(card.dueAt, due),
    milestoneId: card.milestoneId ?? '',
  };
}

//anything left as it was goes back exactly as the api sent it: a due time keeps its minutes and
//a description keeps the api's own html rather than being rebuilt from the markup
function fieldsFrom(values: EditValues, base: TaskCard, due: DueKind): CardFields {
  const start = valuesFrom(base, due);
  const dueAt =
    values.due === start.due
      ? base.dueAt
      : !values.due
        ? null
        : due === 'day'
          ? endOfWorkingDay(values.due)
          : fromSastDateTimeInput(values.due);

  return {
    subject: values.subject,
    description:
      values.description === start.description ? base.description : markupToHtml(values.description),
    priority: values.priority,
    dueAt,
    milestoneId: values.milestoneId || null,
  };
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type EditCardDialogProps = {
  card: TaskCard | null; //open while a card is set
  due: DueKind;
  milestones?: readonly Milestone[]; //event cards can hang off one of the event's milestones
  boardKey: QueryKey;
  onClose: () => void;
};

export function EditCardDialog({ card, ...rest }: EditCardDialogProps) {
  if (!card) return null;
  return <EditForm key={card.cardId} card={card} {...rest} />;
}

function EditForm({ card, due, milestones, boardKey, onClose }: EditCardDialogProps & { card: TaskCard }) {
  const update = useUpdateCard(boardKey);
  const toast = useToast();
  //the version these values started from. it only moves on when the user chooses to keep their
  //changes over someone else's, so a save never quietly replaces a change they haven't seen
  const [base, setBase] = useState(card);
  const [conflict, setConflict] = useState<TaskCard | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<EditValues>({ resolver: zodResolver(editSchema), defaultValues: valuesFrom(card, due) });

  async function onSubmit(values: EditValues) {
    setServerError(null);
    try {
      const saved = await update.mutateAsync({
        card,
        rowVersion: base.rowVersion,
        fields: fieldsFrom(values, base, due),
      });
      toast.success(`Saved your changes to ${saved.subject}.`);
      onClose();
    } catch (error) {
      const current = currentCardIn(error);
      if (current) {
        setConflict(current);
        return;
      }
      const fieldErrors =
        isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      const mapped = fieldErrors.flatMap(([key, messages]) => {
        const field = formFieldFor[key];
        return field && messages[0] ? [{ field, message: messages[0] }] : [];
      });
      mapped.forEach(({ field, message }) => setError(field, { message }));
      if (mapped.length === 0) setServerError(describeCardError(error, card.subject));
    }
  }

  function keepMine() {
    if (conflict) setBase(conflict);
    setConflict(null);
  }

  return (
    <Dialog open wide title={`Edit ${card.subject}`} onClose={onClose}>
      {conflict && (
        <div className={styles.alert}>
          <Alert tone="warning">
            <p className={styles.alertText}>
              Someone else changed this card while you were editing it, so your changes weren't saved. Close
              this to see their version, or keep your changes and save again to replace it.
            </p>
            <Button onClick={keepMine}>Keep my changes</Button>
          </Alert>
        </div>
      )}
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={styles.fields}>
          <Field label="Subject" required full error={errors.subject?.message}>
            {(field) => <input {...field} {...register('subject')} maxLength={200} autoComplete="off" />}
          </Field>

          <Field label="Description" full hint={formattingHint} error={errors.description?.message}>
            {(field) => (
              <Controller
                control={control}
                name="description"
                render={({ field: description }) => (
                  <DescriptionEditor {...field} {...description} maxLength={5000} />
                )}
              />
            )}
          </Field>

          <Field label="Priority" error={errors.priority?.message}>
            {(field) => (
              <select {...field} {...register('priority')}>
                {priorities.map((priority) => (
                  <option key={priority} value={priority}>
                    {priority}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <Field label="Due" error={errors.due?.message}>
            {(field) => (
              <input {...field} {...register('due')} type={due === 'day' ? 'date' : 'datetime-local'} />
            )}
          </Field>

          {milestones && (
            <Field label="Milestone" full error={errors.milestoneId?.message}>
              {(field) => (
                <select {...field} {...register('milestoneId')}>
                  <option value="">None</option>
                  {milestones.map((milestone) => (
                    <option key={milestone.milestoneId} value={milestone.milestoneId}>
                      {milestoneLabels[milestone.milestoneType]} · {formatDateTime(milestone.scheduledStart)}
                    </option>
                  ))}
                </select>
              )}
            </Field>
          )}
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting} disabled={conflict !== null}>
            Save changes
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
