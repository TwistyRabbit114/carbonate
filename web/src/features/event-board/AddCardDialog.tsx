import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { Board, Milestone } from '@/api/types';
import { useSignedIn } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { DescriptionEditor, formattingHint } from '@/components/DescriptionEditor';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { cardPriorities as priorities } from '@/features/boards/cards';
import { PeoplePicker } from '@/features/boards/PeoplePicker';
import { milestoneLabels } from '@/features/events/labels';
import { formatDateTime, fromSastDateTimeInput } from '@/lib/format';
import { markupToHtml } from '@/lib/richText';
import { useAddCard, useCrewPeople } from './api';
import styles from '@/features/boards/CardDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const addSchema = z.object({
  subject: z
    .string()
    .trim()
    .min(1, 'Give the card a subject.')
    .max(200, 'Keep the subject under 200 characters.'),
  description: z.string().max(5000, 'Keep the description under 5 000 characters.'),
  priority: z.enum(priorities),
  due: z.string(), //yyyy-mm-ddThh:mm in SAST, or empty
  milestoneId: z.string(),
  columnId: z.string().min(1, 'Choose a column.'),
  assigneeIds: z.array(z.string()),
});

type AddValues = z.infer<typeof addSchema>;

const formFieldFor: Record<string, keyof AddValues> = {
  subject: 'subject',
  description: 'description',
  priority: 'priority',
  dueAt: 'due',
  milestoneId: 'milestoneId',
  columnId: 'columnId',
  assigneeIds: 'assigneeIds',
};

function describeAddError(error: unknown) {
  if (!isApiError(error)) return "Something went wrong, so the card wasn't added. Try again.";
  if (error.status === 0) return "Couldn't reach Carbonate, so the card wasn't added. Try again.";
  if (error.status === 403) return "Your role can't add cards to this board.";
  return error.problem.detail ?? "Something went wrong, so the card wasn't added. Try again.";
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type AddCardDialogProps = {
  open: boolean;
  eventId: string;
  board: Board;
  milestones: readonly Milestone[] | undefined;
  onClose: () => void;
};

export function AddCardDialog({ open, ...rest }: AddCardDialogProps) {
  if (!open) return null;
  return <AddForm {...rest} />;
}

//new cards go to the bottom of their column. people come from the event's crew, the only people
//the api lets a card go to
function AddForm({ eventId, board, milestones, onClose }: Omit<AddCardDialogProps, 'open'>) {
  const me = useSignedIn().user.userId;
  const crew = useCrewPeople(eventId);
  const add = useAddCard(eventId, board.boardId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const firstColumn = [...board.columns].sort((a, b) => a.position - b.position)[0];
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<AddValues>({
    resolver: zodResolver(addSchema),
    defaultValues: {
      subject: '',
      description: '',
      priority: 'Normal',
      due: '',
      milestoneId: '',
      columnId: firstColumn?.columnId ?? '',
      assigneeIds: [],
    },
  });

  async function onSubmit(values: AddValues) {
    setServerError(null);
    try {
      const card = await add.mutateAsync({
        subject: values.subject,
        description: markupToHtml(values.description),
        priority: values.priority,
        dueAt: values.due ? fromSastDateTimeInput(values.due) : null,
        milestoneId: values.milestoneId || null,
        columnId: values.columnId,
        assigneeIds: values.assigneeIds,
      });
      const column = board.columns.find((candidate) => candidate.columnId === values.columnId);
      toast.success(column ? `${card.subject} was added to ${column.name}.` : `${card.subject} was added.`);
      onClose();
    } catch (error) {
      const fieldErrors =
        isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      const mapped = fieldErrors.flatMap(([key, messages]) => {
        const field = formFieldFor[key.replace(/\[\d+\]$/, '')];
        return field && messages[0] ? [{ field, message: messages[0] }] : [];
      });
      mapped.forEach(({ field, message }) => setError(field, { message }));
      if (mapped.length === 0) setServerError(describeAddError(error));
    }
  }

  return (
    <Dialog open wide title="Add a card" onClose={onClose}>
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

          <Field label="Column" required error={errors.columnId?.message}>
            {(field) => (
              <select {...field} {...register('columnId')}>
                {board.columns.map((column) => (
                  <option key={column.columnId} value={column.columnId}>
                    {column.name}
                  </option>
                ))}
              </select>
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
            {(field) => <input {...field} {...register('due')} type="datetime-local" />}
          </Field>

          <Field label="Milestone" error={errors.milestoneId?.message}>
            {(field) => (
              <select {...field} {...register('milestoneId')} disabled={!milestones}>
                <option value="">None</option>
                {milestones?.map((milestone) => (
                  <option key={milestone.milestoneId} value={milestone.milestoneId}>
                    {milestoneLabels[milestone.milestoneType]} · {formatDateTime(milestone.scheduledStart)}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <div className={styles.full}>
            <Controller
              control={control}
              name="assigneeIds"
              render={({ field }) => (
                <PeoplePicker
                  legend="Assign to"
                  people={crew.people}
                  failed={crew.failed}
                  selected={field.value}
                  onChange={field.onChange}
                  me={me}
                  hint="Only people on the event's crew are listed. Add someone to the crew first to give them a card."
                  error={errors.assigneeIds?.message}
                />
              )}
            />
          </div>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Add card
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
