import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useNavigate, useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import type { EventDetail } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { BackLink } from '@/components/BackLink';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Field } from '@/components/Field';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { useToast } from '@/components/toast/ToastContext';
import { useVenues } from '@/features/venues/api';
import { currentEventIn, useClients, useCreateEvent, useDivisions, useEvent, useUpdateEvent } from './api';
import { MissingEvent } from './MissingEvent';
import {
  blankEvent,
  eventSchema,
  eventTypes,
  formFieldFor,
  requestFromValues,
  valuesFromEvent,
  type EventFormValues,
} from './eventForm';
import { eventTypeLabels, infrastructureLabels, paymentModeLabels } from './labels';
import styles from './EventFormPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//one form for a new booking and for changing one (FR-01). a new event starts as an enquiry, with
//its milestones, task board and stock list set up from the template (NFR-03)
export default function EventFormPage() {
  const { eventId } = useParams();
  return eventId ? <EditEvent eventId={eventId} /> : <EventForm />;
}

function EditEvent({ eventId }: { eventId: string }) {
  const event = useEvent(eventId);

  if (event.isError && isApiError(event.error) && event.error.status === 404) return <MissingEvent />;
  if (event.isError) {
    return (
      <>
        <PageHead title="Edit event" eyebrow={<BackLink to={`/events/${eventId}`}>Event</BackLink>} />
        <ErrorState message="We couldn't load this event." onRetry={() => void event.refetch()} />
      </>
    );
  }
  if (!event.data) return <PageSkeleton />;
  return <EventForm key={event.data.eventId} event={event.data} />;
}

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

function describeSaveError(error: unknown) {
  if (!isApiError(error)) return "Something went wrong, so it wasn't saved. Try again.";
  if (error.status === 0)
    return "Couldn't reach Carbonate, so it wasn't saved. Check your connection and try again.";
  if (error.status === 403) return error.problem.detail ?? "Your role can't make this change.";
  if (error.status === 404) return 'This event has been removed, so it can no longer be changed.';
  return error.problem.detail ?? "Something went wrong, so it wasn't saved. Try again.";
}

function EventForm({ event }: { event?: EventDetail }) {
  const check = usePermissions();
  const navigate = useNavigate();
  const toast = useToast();
  const create = useCreateEvent();
  const update = useUpdateEvent(event?.eventId ?? '');
  const clients = useClients();
  const venues = useVenues();
  const divisions = useDivisions();

  //the budget is $price: only someone who can see it gets the box (plan section 7.3)
  const canSetBudget = check.can('finance.view_client_price');
  //the version the values started from. it only moves on when the user chooses, so a save never
  //quietly replaces a change they haven't seen (NFR-15)
  const [base, setBase] = useState(event);
  const [conflict, setConflict] = useState<EventDetail | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<EventFormValues>({
    resolver: zodResolver(eventSchema),
    defaultValues: event ? valuesFromEvent(event) : blankEvent,
  });

  const backTo = event ? `/events/${event.eventId}` : '/events';

  async function onSubmit(values: EventFormValues) {
    setServerError(null);
    try {
      if (base) {
        const saved = await update.mutateAsync({
          ...requestFromValues(values, { canSetBudget, base }),
          rowVersion: base.rowVersion,
        });
        toast.success(`Saved your changes to ${saved.name}.`);
        void navigate(`/events/${saved.eventId}`);
      } else {
        const created = await create.mutateAsync(requestFromValues(values, { canSetBudget }));
        toast.success(
          `${created.name} is set up with its milestones, task board and stock list. It goes on the events board once the PO or deposit is recorded.`,
        );
        void navigate(`/events/${created.eventId}`);
      }
    } catch (error) {
      const current = currentEventIn(error);
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
      if (mapped.length === 0) setServerError(describeSaveError(error));
    }
  }

  //their version replaces what's in the form
  function reload() {
    if (!conflict) return;
    reset(valuesFromEvent(conflict));
    setBase(conflict);
    setConflict(null);
  }

  //the form keeps what the user typed, now based on their version, ready to check and save again
  function keepMine() {
    if (!conflict) return;
    setBase(conflict);
    setConflict(null);
  }

  const lookupsFailed = clients.isError || venues.isError || divisions.isError;

  return (
    <>
      <PageHead
        eyebrow={<BackLink to={backTo}>{event ? event.name : 'Events'}</BackLink>}
        title={event ? 'Edit event' : 'New event'}
      />

      {conflict && (
        <div className={styles.alert}>
          <Alert tone="warning">
            <p className={styles.alertText}>
              Someone else changed this event while you were editing it, so your changes weren't saved. Load
              their version, or keep your changes and save again to replace it.
            </p>
            <div className={styles.alertButtons}>
              <Button onClick={reload}>Load their version</Button>
              <Button onClick={keepMine}>Keep my changes</Button>
            </div>
          </Alert>
        </div>
      )}
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      {lookupsFailed && (
        <div className={styles.alert}>
          <Alert tone="danger">
            Some of the lists on this form didn't load, so it can't be saved yet. Refresh the page to try
            again.
          </Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className={styles.form}>
        <Panel title="Booking">
          <div className={styles.fields}>
            <Field label="Event name" required full error={errors.name?.message}>
              {(field) => <input {...field} {...register('name')} maxLength={200} autoComplete="off" />}
            </Field>

            <Field
              label="Event code"
              required
              hint="4 to 20 letters, numbers and dashes, like NAI-WED-26."
              error={errors.eventCode?.message}
            >
              {(field) => (
                <input
                  {...field}
                  {...register('eventCode')}
                  maxLength={20}
                  autoComplete="off"
                  spellCheck={false}
                />
              )}
            </Field>

            {/*TODO(plan): adding a new client from here needs a create endpoint from C*/}
            <Field label="Client" required error={errors.clientId?.message}>
              {(field) => (
                <select {...field} {...register('clientId')} disabled={!clients.data}>
                  <option value="">{clients.isPending ? 'Loading clients…' : 'Choose a client'}</option>
                  {clients.data?.map((client) => (
                    <option key={client.clientId} value={client.clientId}>
                      {client.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            {/*TODO(plan): adding a venue from here comes with the venue screens in settings*/}
            <Field label="Venue" required error={errors.venueId?.message}>
              {(field) => (
                <select {...field} {...register('venueId')} disabled={!venues.data}>
                  <option value="">{venues.isPending ? 'Loading venues…' : 'Choose a venue'}</option>
                  {venues.data?.map((venue) => (
                    <option key={venue.venueId} value={venue.venueId}>
                      {venue.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            <Field label="Division" required error={errors.divisionId?.message}>
              {(field) => (
                <select {...field} {...register('divisionId')} disabled={!divisions.data}>
                  <option value="">{divisions.isPending ? 'Loading…' : 'Choose a division'}</option>
                  {divisions.data?.map((division) => (
                    <option key={division.divisionId} value={division.divisionId}>
                      {division.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            <Field label="Event type" required error={errors.eventType?.message}>
              {(field) => (
                <select {...field} {...register('eventType')}>
                  {eventTypes.map((type) => (
                    <option key={type} value={type}>
                      {eventTypeLabels[type]}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            <Field label="Event date" required error={errors.eventDate?.message}>
              {(field) => <input {...field} {...register('eventDate')} type="date" />}
            </Field>

            <Field
              label="Starts"
              required
              hint="When doors open. The event goes live on its own at this time."
              error={errors.startsAt?.message}
            >
              {(field) => <input {...field} {...register('startsAt')} type="datetime-local" />}
            </Field>

            <Field
              label="Ends"
              required
              hint="When it closes. It's marked finished on its own at this time."
              error={errors.endsAt?.message}
            >
              {(field) => <input {...field} {...register('endsAt')} type="datetime-local" />}
            </Field>

            <Field label="Pack size (estimated)" required error={errors.packSizeEstimated?.message}>
              {(field) => (
                <input
                  {...field}
                  {...register('packSizeEstimated')}
                  type="number"
                  min={0}
                  inputMode="numeric"
                />
              )}
            </Field>

            <Field label="Expected headcount" error={errors.headcountExpected?.message}>
              {(field) => (
                <input
                  {...field}
                  {...register('headcountExpected')}
                  type="number"
                  min={0}
                  inputMode="numeric"
                />
              )}
            </Field>

            <Field label="Staff required" required error={errors.staffRequired?.message}>
              {(field) => (
                <input {...field} {...register('staffRequired')} type="number" min={0} inputMode="numeric" />
              )}
            </Field>

            {canSetBudget && (
              <Field label="Budget" hint="In rand. Optional." error={errors.budget?.message}>
                {(field) => (
                  <input {...field} {...register('budget')} inputMode="decimal" autoComplete="off" />
                )}
              </Field>
            )}

            <Field label="Payment" required error={errors.paymentMode?.message}>
              {(field) => (
                <select {...field} {...register('paymentMode')}>
                  {(['PurchaseOrder', 'Deposit'] as const).map((mode) => (
                    <option key={mode} value={mode}>
                      {paymentModeLabels[mode]}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            {/*TODO(plan): the prototype also offers Mixed, FR-01 only owned or rented. check at UAT*/}
            <Field label="Infrastructure" required error={errors.infrastructureMode?.message}>
              {(field) => (
                <select {...field} {...register('infrastructureMode')}>
                  {(['Owned', 'Rented'] as const).map((mode) => (
                    <option key={mode} value={mode}>
                      {infrastructureLabels[mode]}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            <Field label="Confidential" required full error={errors.confidential?.message}>
              {(field) => (
                <select {...field} {...register('confidential')}>
                  <option value="no">No</option>
                  <option value="yes">Yes, NDA in effect</option>
                </select>
              )}
            </Field>
          </div>
        </Panel>

        <div className={styles.buttons}>
          <LinkButton to={backTo}>Cancel</LinkButton>
          <Button
            type="submit"
            variant="primary"
            busy={isSubmitting}
            disabled={conflict !== null || lookupsFailed}
          >
            {event ? 'Save changes' : 'Create event'}
          </Button>
        </div>
      </form>
    </>
  );
}
