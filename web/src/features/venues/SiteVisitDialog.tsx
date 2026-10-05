import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { SiteVisit, SiteVisitRequest } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { useSaveSiteVisit } from './api';
import form from '@/components/FormLayout.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//the api's lengths, so a long note is caught here first. everything but the date is optional,
//a recce writes down what it found (FR-33)
const text = (max: number) => z.string().trim().max(max, `Keep it under ${max} characters.`);

const visitSchema = z.object({
  visitDate: z.string().min(1, 'Enter the date of the visit.'),
  vehicleType: text(100),
  licencePlate: text(20).refine(
    (value) => value === '' || /^[A-Za-z0-9 -]+$/.test(value),
    'Use letters, numbers, spaces and dashes only, like CA 123-456.',
  ),
  driverName: text(200),
  requiredDriverDetails: text(500),
  crewNames: text(1000),
  signInProcedure: text(2000),
  securityCheckpoint: text(500),
  healthSafetyFileRef: text(200),
  notes: text(2000),
});

type VisitValues = z.infer<typeof visitSchema>;
type VisitField = keyof VisitValues;

const visitFields = Object.keys(visitSchema.shape) as VisitField[];

function valuesFrom(visit: SiteVisit | null): VisitValues {
  return {
    visitDate: visit?.visitDate ?? '',
    vehicleType: visit?.vehicleType ?? '',
    licencePlate: visit?.licencePlate ?? '',
    driverName: visit?.driverName ?? '',
    requiredDriverDetails: visit?.requiredDriverDetails ?? '',
    crewNames: visit?.crewNames ?? '',
    signInProcedure: visit?.signInProcedure ?? '',
    securityCheckpoint: visit?.securityCheckpoint ?? '',
    healthSafetyFileRef: visit?.healthSafetyFileRef ?? '',
    notes: visit?.notes ?? '',
  };
}

//empty boxes go as null. the conductor is left to the api: whoever adds the recce, or whoever
//did it already when it's being corrected
function requestFrom(values: VisitValues): SiteVisitRequest {
  const orNull = (value: string) => value || null;
  return {
    visitDate: values.visitDate,
    conductedByUserId: null,
    vehicleType: orNull(values.vehicleType),
    licencePlate: orNull(values.licencePlate),
    driverName: orNull(values.driverName),
    requiredDriverDetails: orNull(values.requiredDriverDetails),
    crewNames: orNull(values.crewNames),
    signInProcedure: orNull(values.signInProcedure),
    securityCheckpoint: orNull(values.securityCheckpoint),
    healthSafetyFileRef: orNull(values.healthSafetyFileRef),
    notes: orNull(values.notes),
  };
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type SiteVisitDialogProps = {
  eventId: string;
  visit: SiteVisit | null; //null adds a new one
  onClose: () => void;
};

export function SiteVisitDialog({ eventId, visit, onClose }: SiteVisitDialogProps) {
  const save = useSaveSiteVisit(eventId, visit?.siteVisitId ?? null);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<VisitValues>({ resolver: zodResolver(visitSchema), defaultValues: valuesFrom(visit) });

  async function onSubmit(values: VisitValues) {
    setServerError(null);
    try {
      await save.mutateAsync(requestFrom(values));
      toast.success(visit ? 'The recce notes are updated.' : 'The recce is added.');
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        visitFields.includes(key as VisitField) ? (key as VisitField) : undefined,
      );
      if (!landed)
        setServerError(problemMessage(error, "The recce wasn't saved.", "Your role can't record recces."));
    }
  }

  const textField = (name: VisitField, label: string, max: number, long = false) => (
    <Field label={label} error={errors[name]?.message} full={long}>
      {(field) =>
        long ? (
          <textarea {...field} {...register(name)} maxLength={max} rows={3} />
        ) : (
          <input {...field} {...register(name)} maxLength={max} autoComplete="off" />
        )
      }
    </Field>
  );

  return (
    <Dialog open wide title={visit ? 'Edit recce' : 'Add recce'} onClose={onClose}>
      <p className={form.lead}>What the site visit found, so the crew know how to get in on the day.</p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Visit date" required error={errors.visitDate?.message}>
            {(field) => <input {...field} {...register('visitDate')} type="date" />}
          </Field>
          {textField('healthSafetyFileRef', 'H&S file reference', 200)}
          {textField('vehicleType', 'Vehicle', 100)}
          {textField('licencePlate', 'Licence plate', 20)}
          {textField('driverName', 'Driver', 200)}
          {textField('securityCheckpoint', 'Security checkpoint', 500)}
          {textField('requiredDriverDetails', 'Driver details security needs', 500, true)}
          {textField('crewNames', 'Crew going in', 1000, true)}
          {textField('signInProcedure', 'Sign-in procedure', 2000, true)}
          {textField('notes', 'Notes', 2000, true)}
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            {visit ? 'Save changes' : 'Add recce'}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
