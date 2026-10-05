import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { Venue, VenueRequest } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { formatClock } from '@/lib/format';
import { useSaveVenue } from './api';
import form from '@/components/FormLayout.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//the api's rules: a name and an address, and opening hours either both set or both empty. a
//closing time earlier than the opening one means the venue is open past midnight
const text = (max: number) => z.string().trim().max(max, `Keep it under ${max} characters.`);

const venueSchema = z
  .object({
    name: text(200).min(1, "Enter the venue's name."),
    address: text(500).min(1, "Enter the venue's address."),
    accessRoute: text(2000),
    loadingBayDetails: text(1000),
    operatingHoursStart: z.string(),
    operatingHoursEnd: z.string(),
    requiresSecurityClearance: z.boolean(),
    requiresHealthSafetyFile: z.boolean(),
    ppeRequirements: text(1000),
    isActive: z.boolean(),
  })
  .superRefine((values, context) => {
    if (values.operatingHoursStart && !values.operatingHoursEnd) {
      context.addIssue({
        code: 'custom',
        path: ['operatingHoursEnd'],
        message: 'Add the closing time too, or leave both times empty.',
      });
    }
    if (values.operatingHoursEnd && !values.operatingHoursStart) {
      context.addIssue({
        code: 'custom',
        path: ['operatingHoursStart'],
        message: 'Add the opening time too, or leave both times empty.',
      });
    }
  });

type VenueValues = z.infer<typeof venueSchema>;
type VenueField = keyof VenueValues;

const venueFields: VenueField[] = [
  'name',
  'address',
  'accessRoute',
  'loadingBayDetails',
  'operatingHoursStart',
  'operatingHoursEnd',
  'ppeRequirements',
];

function valuesFrom(venue: Venue | null): VenueValues {
  return {
    name: venue?.name ?? '',
    address: venue?.address ?? '',
    accessRoute: venue?.accessRoute ?? '',
    loadingBayDetails: venue?.loadingBayDetails ?? '',
    operatingHoursStart: venue?.operatingHoursStart ? formatClock(venue.operatingHoursStart) : '',
    operatingHoursEnd: venue?.operatingHoursEnd ? formatClock(venue.operatingHoursEnd) : '',
    requiresSecurityClearance: venue?.requiresSecurityClearance ?? false,
    requiresHealthSafetyFile: venue?.requiresHealthSafetyFile ?? false,
    ppeRequirements: venue?.ppeRequirements ?? '',
    isActive: venue?.isActive ?? true,
  };
}

//time inputs give hh:mm, the api takes hh:mm:ss
function requestFrom(values: VenueValues): VenueRequest {
  const orNull = (value: string) => value || null;
  const time = (value: string) => (value ? `${value}:00` : null);
  return {
    name: values.name,
    address: values.address,
    accessRoute: orNull(values.accessRoute),
    loadingBayDetails: orNull(values.loadingBayDetails),
    operatingHoursStart: time(values.operatingHoursStart),
    operatingHoursEnd: time(values.operatingHoursEnd),
    requiresSecurityClearance: values.requiresSecurityClearance,
    requiresHealthSafetyFile: values.requiresHealthSafetyFile,
    ppeRequirements: orNull(values.ppeRequirements),
    isActive: values.isActive,
  };
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type VenueDialogProps = {
  venue: Venue | null; //null adds a new one
  onClose: () => void;
};

//venues are kept and reused from event to event (FR-32). one that's no longer used is switched
//off rather than deleted, so old events still show where they were
export function VenueDialog({ venue, onClose }: VenueDialogProps) {
  const save = useSaveVenue(venue?.venueId ?? null);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<VenueValues>({ resolver: zodResolver(venueSchema), defaultValues: valuesFrom(venue) });

  async function onSubmit(values: VenueValues) {
    setServerError(null);
    try {
      const saved = await save.mutateAsync(requestFrom(values));
      toast.success(venue ? `${saved.name} is updated.` : `${saved.name} is added.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => venueFields.find((field) => field === key));
      if (!landed)
        setServerError(problemMessage(error, "The venue wasn't saved.", "Your role can't change venues."));
    }
  }

  return (
    <Dialog open wide title={venue ? `Edit ${venue.name}` : 'Add venue'} onClose={onClose}>
      <p className={form.lead}>Crew see this on their event, so say how to get in and where to park.</p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Name" required error={errors.name?.message}>
            {(field) => <input {...field} {...register('name')} maxLength={200} autoComplete="off" />}
          </Field>
          <Field label="Address" required error={errors.address?.message}>
            {(field) => <input {...field} {...register('address')} maxLength={500} autoComplete="off" />}
          </Field>
          <Field label="Opens" error={errors.operatingHoursStart?.message}>
            {(field) => <input {...field} {...register('operatingHoursStart')} type="time" />}
          </Field>
          <Field
            label="Closes"
            hint="Earlier than opening means it's open past midnight."
            error={errors.operatingHoursEnd?.message}
          >
            {(field) => <input {...field} {...register('operatingHoursEnd')} type="time" />}
          </Field>
          <Field label="Access route" full error={errors.accessRoute?.message}>
            {(field) => <textarea {...field} {...register('accessRoute')} maxLength={2000} rows={3} />}
          </Field>
          <Field label="Loading bay" full error={errors.loadingBayDetails?.message}>
            {(field) => <textarea {...field} {...register('loadingBayDetails')} maxLength={1000} rows={2} />}
          </Field>
          <Field label="PPE" full error={errors.ppeRequirements?.message}>
            {(field) => <textarea {...field} {...register('ppeRequirements')} maxLength={1000} rows={2} />}
          </Field>
          <fieldset className={`${form.checks} ${form.full}`}>
            <legend className={form.legend}>The venue needs</legend>
            <label className={form.check}>
              <input type="checkbox" {...register('requiresSecurityClearance')} />
              Security clearance
            </label>
            <label className={form.check}>
              <input type="checkbox" {...register('requiresHealthSafetyFile')} />A health and safety file
            </label>
          </fieldset>
          {venue && (
            <fieldset className={`${form.checks} ${form.full}`}>
              <legend className={form.legend}>In use</legend>
              <label className={form.check}>
                <input type="checkbox" {...register('isActive')} />
                Offer this venue for new events
              </label>
            </fieldset>
          )}
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            {venue ? 'Save changes' : 'Add venue'}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
