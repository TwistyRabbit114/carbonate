import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { EventDetail, Milestone, ScheduleResult } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { formatSpan, fromSastDateTimeInput, sastDateTimeInput } from '@/lib/format';
import { useReschedule } from './api';
import { milestoneLabels } from './labels';
import styles from './EventDialogs.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const rescheduleSchema = z
  .object({
    start: z.string().min(1, 'Choose the new start.'),
    end: z.string().min(1, 'Choose the new end.'),
  })
  .refine((values) => values.end >= values.start, {
    path: ['end'],
    message: "It can't end before it starts.",
  });

type RescheduleValues = z.infer<typeof rescheduleSchema>;

//plain words for a reschedule the api turned down
function describeRescheduleError(error: unknown) {
  if (!isApiError(error)) return 'Something went wrong, so nothing moved. Try again.';
  if (error.status === 0)
    return "Couldn't reach Carbonate, so nothing moved. Check your connection and try again.";
  if (error.status === 409) {
    return 'Someone else changed this event while you were looking at it, so nothing moved. Close this to see the latest dates, then try again.';
  }
  //a milestone that has already happened, or a change that would loop the chain back on itself
  if (error.status === 422) return error.problem.detail ?? "That change can't be scheduled.";
  if (error.status === 403) return "Your role can't reschedule milestones.";
  return 'Something went wrong, so nothing moved. Try again.';
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type RescheduleDialogProps = {
  event: EventDetail;
  milestone: Milestone | null; //open while a milestone is set
  milestones: readonly Milestone[];
  onClose: () => void;
};

//moving one milestone moves everything that depends on it (FR-04), so once it's saved the
//dialog says what else moved and from when to when
export function RescheduleDialog({ milestone, ...rest }: RescheduleDialogProps) {
  if (!milestone) return null;
  return <RescheduleForm key={milestone.milestoneId} milestone={milestone} {...rest} />;
}

function RescheduleForm({
  event,
  milestone,
  milestones,
  onClose,
}: RescheduleDialogProps & { milestone: Milestone }) {
  const reschedule = useReschedule(event.eventId);
  //the times as they were when the dialog opened, to compare against once the chain has moved
  const [before] = useState(milestones);
  const [result, setResult] = useState<ScheduleResult | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const label = milestoneLabels[milestone.milestoneType];
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RescheduleValues>({
    resolver: zodResolver(rescheduleSchema),
    defaultValues: {
      start: sastDateTimeInput(milestone.scheduledStart),
      end: sastDateTimeInput(milestone.scheduledEnd),
    },
  });

  async function onSubmit(values: RescheduleValues) {
    setServerError(null);
    try {
      setResult(
        await reschedule.mutateAsync({
          milestoneId: milestone.milestoneId,
          newStart: fromSastDateTimeInput(values.start),
          newEnd: fromSastDateTimeInput(values.end),
          rowVersion: event.rowVersion,
        }),
      );
    } catch (error) {
      const fields = isApiError(error) && error.status === 400 ? (error.problem.errors ?? {}) : {};
      if (fields.newStart?.[0]) setError('start', { message: fields.newStart[0] });
      if (fields.newEnd?.[0]) setError('end', { message: fields.newEnd[0] });
      if (!fields.newStart && !fields.newEnd) setServerError(describeRescheduleError(error));
    }
  }

  if (result) {
    const others = result.milestones.filter((moved) => moved.milestoneId !== milestone.milestoneId);
    return (
      <Dialog open title={`${label} rescheduled`} onClose={onClose}>
        <p className={styles.lead}>
          {others.length === 0
            ? `Only ${label.toLowerCase()} moved, nothing else depends on it.`
            : `Moving ${label.toLowerCase()} moved ${others.length} other ${others.length === 1 ? 'milestone' : 'milestones'} with it.`}
        </p>
        <ul className={styles.moved} aria-label="What moved">
          {result.milestones.map((moved) => {
            const was = before.find((candidate) => candidate.milestoneId === moved.milestoneId);
            return (
              <li key={moved.milestoneId}>
                <strong>{milestoneLabels[moved.milestoneType]}</strong>
                <br />
                Now {formatSpan(moved.scheduledStart, moved.scheduledEnd)}
                {was && (
                  <>
                    <br />
                    <span className={styles.was}>Was {formatSpan(was.scheduledStart, was.scheduledEnd)}</span>
                  </>
                )}
              </li>
            );
          })}
        </ul>
        <DialogFooter>
          <Button variant="primary" onClick={onClose}>
            Done
          </Button>
        </DialogFooter>
      </Dialog>
    );
  }

  return (
    <Dialog open title={`Reschedule ${label.toLowerCase()}`} onClose={onClose}>
      <p className={styles.lead}>
        Anything that follows it moves by the same amount. Times are Cape Town time.
      </p>
      {serverError && (
        <div className={styles.alert}>
          <Alert
            tone={isApiError(reschedule.error) && reschedule.error.status === 409 ? 'warning' : 'danger'}
          >
            {serverError}
          </Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={styles.fields}>
          <Field label="Starts" required error={errors.start?.message}>
            {(field) => <input {...field} {...register('start')} type="datetime-local" />}
          </Field>
          <Field label="Ends" required error={errors.end?.message}>
            {(field) => <input {...field} {...register('end')} type="datetime-local" />}
          </Field>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Reschedule
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
