import { useId, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { EventDetail } from '@/api/types';
import { describeRoles } from '@/auth/roles';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { useActiveUsers } from '@/features/admin-tasks/api';
import { fromSastDateTimeInput, sastDateTimeInput } from '@/lib/format';
import { useAssignCrew } from './api';
import styles from './EventDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const crewSchema = z
  .object({
    userId: z.string().min(1, 'Choose who is working.'),
    crewRole: z
      .string()
      .trim()
      .min(1, 'Say what they are doing, like Bartender.')
      .max(50, 'Keep it under 50 characters.'),
    shiftStart: z.string().min(1, 'Choose when the shift starts.'),
    shiftEnd: z.string().min(1, 'Choose when the shift ends.'),
  })
  .refine((values) => values.shiftEnd > values.shiftStart, {
    path: ['shiftEnd'],
    message: 'The shift must end after it starts.',
  });

type CrewValues = z.infer<typeof crewSchema>;

//the staff rate card's roles (appendix d), as suggestions. any other role can be typed
const roleSuggestions = ['Bar lead', 'Bartender', 'Waiter', 'Bar support', 'Manager'];

const formFieldFor: Record<string, keyof CrewValues> = {
  userId: 'userId',
  crewRole: 'crewRole',
  shiftStart: 'shiftStart',
  shiftEnd: 'shiftEnd',
};

function describeCrewError(error: unknown) {
  if (!isApiError(error)) return "Something went wrong, so they weren't added. Try again.";
  if (error.status === 0) return "Couldn't reach Carbonate, so they weren't added. Try again.";
  if (error.status === 403) return "Your role can't put people on an event's crew.";
  return error.problem.detail ?? "Something went wrong, so they weren't added. Try again.";
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type AssignCrewDialogProps = {
  event: EventDetail;
  onClose: () => void;
};

//puts someone on the event for a shift (FR-07). the shift starts as the event's live window
//TODO(plan): hourly rates can only be set by roles that see every rate, and /api/me doesn't say
//whether staff cost is everyone's or own only, so the rate isn't offered here yet
export function AssignCrewDialog({ event, onClose }: AssignCrewDialogProps) {
  const people = useActiveUsers();
  const assign = useAssignCrew(event.eventId);
  const toast = useToast();
  const suggestionsId = useId();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CrewValues>({
    resolver: zodResolver(crewSchema),
    defaultValues: {
      userId: '',
      crewRole: '',
      shiftStart: sastDateTimeInput(event.startsAt),
      shiftEnd: sastDateTimeInput(event.endsAt),
    },
  });

  async function onSubmit(values: CrewValues) {
    setServerError(null);
    try {
      const shift = await assign.mutateAsync({
        userId: values.userId,
        crewRole: values.crewRole,
        shiftStart: fromSastDateTimeInput(values.shiftStart),
        shiftEnd: fromSastDateTimeInput(values.shiftEnd),
      });
      toast.success(`${shift.fullName} is on the crew as ${shift.crewRole.toLowerCase()}.`);
      onClose();
    } catch (error) {
      const fieldErrors =
        isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      const mapped = fieldErrors.flatMap(([key, messages]) => {
        const field = formFieldFor[key];
        return field && messages[0] ? [{ field, message: messages[0] }] : [];
      });
      mapped.forEach(({ field, message }) => setError(field, { message }));
      if (mapped.length === 0) setServerError(describeCrewError(error));
    }
  }

  //TODO(plan): the people list takes user.manage, which the event manager doesn't hold, so they
  //can't pick anyone yet. ask C for a list crew.assign can read
  const peopleBlocked = people.isError && isApiError(people.error) && people.error.status === 403;

  return (
    <Dialog open wide title={`Assign crew to ${event.name}`} onClose={onClose}>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      {peopleBlocked && (
        <div className={styles.alert}>
          <Alert tone="warning">
            The people list isn't open to your role yet, so nobody can be picked here. Ask the Director or the
            Operations Manager to add them for now.
          </Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={styles.fields}>
          <Field label="Person" required error={errors.userId?.message}>
            {(field) => (
              <select {...field} {...register('userId')} disabled={!people.data}>
                <option value="">{people.isPending ? 'Loading people…' : 'Choose someone'}</option>
                {people.data?.map((person) => (
                  <option key={person.userId} value={person.userId}>
                    {person.fullName} · {describeRoles(person.roles)}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <Field label="Crew role" required error={errors.crewRole?.message}>
            {(field) => (
              <>
                <input
                  {...field}
                  {...register('crewRole')}
                  list={suggestionsId}
                  maxLength={50}
                  autoComplete="off"
                />
                <datalist id={suggestionsId}>
                  {roleSuggestions.map((role) => (
                    <option key={role} value={role} />
                  ))}
                </datalist>
              </>
            )}
          </Field>

          <Field label="Shift starts" required error={errors.shiftStart?.message}>
            {(field) => <input {...field} {...register('shiftStart')} type="datetime-local" />}
          </Field>

          <Field label="Shift ends" required error={errors.shiftEnd?.message}>
            {(field) => <input {...field} {...register('shiftEnd')} type="datetime-local" />}
          </Field>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting} disabled={!people.data}>
            Add to crew
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
