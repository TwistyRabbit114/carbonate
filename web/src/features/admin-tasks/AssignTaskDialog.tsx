import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import { useSignedIn } from '@/auth/AuthContext';
import { describeRoles } from '@/auth/roles';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { DescriptionEditor, formattingHint } from '@/components/DescriptionEditor';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { cardPriorities, endOfWorkingDay } from '@/features/boards/cards';
import { sentence } from '@/lib/format';
import { markupToHtml } from '@/lib/richText';
import { useActiveUsers, useAssignTask } from './api';
import styles from './TaskDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const priorities = cardPriorities;

const assignSchema = z.object({
  subject: z.string().trim().min(1, 'Give the task a subject.').max(200, 'Keep the subject under 200 characters.'),
  description: z.string().max(5000, 'Keep the description under 5 000 characters.'),
  priority: z.enum(priorities),
  dueDate: z.string(), //yyyy-mm-dd from the date input, or empty
  assigneeId: z.string().min(1, 'Choose who the task is for.'),
});

type AssignValues = z.infer<typeof assignSchema>;

//the api's field names mapped onto this form's, so its errors land against the right field
const formFieldFor: Record<string, keyof AssignValues> = {
  subject: 'subject',
  description: 'description',
  priority: 'priority',
  dueAt: 'dueDate',
  assigneeIds: 'assigneeId',
};

function describeAssignError(error: unknown) {
  if (!isApiError(error)) return "Something went wrong, so the task wasn't assigned. Try again.";
  if (error.status === 0) return "Couldn't reach Carbonate, so the task wasn't assigned. Try again.";
  if (error.status === 403) return 'Only the Director and Operations Manager can hand out admin tasks.';
  return error.problem.detail ?? "Something went wrong, so the task wasn't assigned. Try again.";
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type AssignTaskDialogProps = {
  open: boolean;
  boardId: string;
  onClose: () => void;
};

//new admin tasks always start in Assigned with someone to do them (FR-19). the due date goes
//to the google calendar from the api's side
export function AssignTaskDialog({ open, boardId, onClose }: AssignTaskDialogProps) {
  if (!open) return null;
  return <AssignForm boardId={boardId} onClose={onClose} />;
}

function AssignForm({ boardId, onClose }: { boardId: string; onClose: () => void }) {
  const me = useSignedIn().user.userId;
  const people = useActiveUsers();
  const assign = useAssignTask(boardId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<AssignValues>({
    resolver: zodResolver(assignSchema),
    defaultValues: { subject: '', description: '', priority: 'Normal', dueDate: '', assigneeId: '' },
  });
  const assigneeId = useWatch({ control, name: 'assigneeId' });

  async function onSubmit(values: AssignValues) {
    setServerError(null);
    try {
      const card = await assign.mutateAsync({
        subject: values.subject,
        description: markupToHtml(values.description),
        priority: values.priority,
        dueAt: endOfWorkingDay(values.dueDate),
        assigneeIds: [values.assigneeId],
      });
      const name = people.data?.find((person) => person.userId === values.assigneeId)?.fullName;
      toast.success(sentence(name ? `${card.subject} was assigned to ${name}` : `${card.subject} was assigned`));
      onClose();
    } catch (error) {
      const fieldErrors = isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      //collection errors come back keyed like assigneeIds[0]
      const mapped = fieldErrors.flatMap(([key, messages]) => {
        const field = formFieldFor[key.replace(/\[\d+\]$/, '')];
        return field && messages[0] ? [{ field, message: messages[0] }] : [];
      });
      mapped.forEach(({ field, message }) => setError(field, { message }));
      if (mapped.length === 0) setServerError(describeAssignError(error));
    }
  }

  //the api lets a manager hand a task to themselves, but they can't then sign it off (FR-20)
  const selfHint =
    assigneeId === me ? "You won't be able to sign off a task you assign to yourself." : undefined;

  return (
    <Dialog open wide title="Assign a task" onClose={onClose}>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      {people.isError && (
        <div className={styles.alert}>
          <Alert tone="danger">Couldn't load the list of people. Close this and try again.</Alert>
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

          <Field label="Due" error={errors.dueDate?.message}>
            {(field) => <input {...field} {...register('dueDate')} type="date" />}
          </Field>

          <Field label="Assign to" required full hint={selfHint} error={errors.assigneeId?.message}>
            {(field) => (
              <select {...field} {...register('assigneeId')} disabled={!people.data}>
                <option value="">{people.isPending ? 'Loading people…' : 'Choose someone'}</option>
                {people.data?.map((person) => (
                  <option key={person.userId} value={person.userId}>
                    {person.fullName} · {describeRoles(person.roles)}
                  </option>
                ))}
              </select>
            )}
          </Field>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Assign task
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
