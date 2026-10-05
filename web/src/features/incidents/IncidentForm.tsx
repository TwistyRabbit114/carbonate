import { useState, type ChangeEvent } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { CircleCheck } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { isApiError } from '@/api/problem';
import type { IncidentType } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { LinkButton } from '@/components/LinkButton';
import { useEquipment, useStockItems } from '@/features/stock/api';
import { useReportIncident } from './api';
import { incidentTypeLabels } from './labels';
import styles from './IncidentForm.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

const incidentTypes = [
  'Breakage',
  'EquipmentFailure',
  'StockShortfall',
] as const satisfies readonly IncidentType[];

//what each choice covers, from the client's common failures: glassware, ice machines, electricals
const typeHints: Record<IncidentType, string> = {
  Breakage: 'Glasses, bar kit, anything broken',
  EquipmentFailure: 'Ice machine, fridge, electrics',
  StockShortfall: 'Ran out, or never arrived',
};

const incidentSchema = z.object({
  incidentType: z.enum(incidentTypes, { message: 'Choose what kind of problem it was.' }),
  //"item:<id>" or "asset:<id>", one select covers both
  subject: z.string().min(1, "Choose what it's about."),
  quantity: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || (/^\d+$/.test(value) && Number(value) >= 1),
      'Enter a whole number, 1 or more.',
    ),
  description: z.string().trim().min(1, 'Say what happened.').max(2000, 'Keep it under 2 000 characters.'),
});

type IncidentValues = z.infer<typeof incidentSchema>;

const blank = { incidentType: undefined, subject: '', quantity: '', description: '' };

const formFieldFor: Record<string, keyof IncidentValues> = {
  incidentType: 'incidentType',
  stockItemId: 'subject',
  assetId: 'subject',
  quantity: 'quantity',
  description: 'description',
};

//the api's cap (plan section 7.5). images only, a phone's camera gives jpeg or heic
const maxPhotoBytes = 10 * 1024 * 1024;

function describeReportError(error: unknown) {
  if (isApiError(error) && error.status === 403) return "Your role can't report incidents on this event.";
  if (isApiError(error) && error.status === 404) return "This event isn't open to you any more.";
  if (isApiError(error) && error.problem.detail && error.status !== 0) return error.problem.detail;
  return "It didn't send. Everything you filled in is still here, so try again when you have signal.";
}

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

type IncidentFormProps = {
  eventId: string;
  doneTo: string; //where "Done" goes after reporting
};

//quick entry from a phone (FR-31): big targets, one column, and on a weak signal nothing is
//lost if the send fails
export function IncidentForm({ eventId, doneTo }: IncidentFormProps) {
  const items = useStockItems();
  const equipment = useEquipment();
  const report = useReportIncident();
  const [photo, setPhoto] = useState<{ file: File; preview: string } | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<IncidentValues>({ resolver: zodResolver(incidentSchema), defaultValues: blank });

  //TODO(plan): casual crew report incidents but D's item and equipment lists take stock.view,
  //which they don't hold, so they can't pick what broke. ask D for a list incident.create can read
  const listsBlocked = [items.error, equipment.error].some(
    (error) => isApiError(error) && error.status === 403,
  );
  const listsFailed = !listsBlocked && (items.isError || equipment.isError);

  function choosePhoto(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    setPhotoError(null);
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      setPhotoError('That file is not a photo.');
      return;
    }
    if (file.size > maxPhotoBytes) {
      setPhotoError('That photo is over 10 MB. Try a smaller one.');
      return;
    }
    //a data url, the content security policy allows those for images and not blob urls
    const reader = new FileReader();
    reader.onload = () => setPhoto({ file, preview: String(reader.result) });
    reader.readAsDataURL(file);
  }

  async function onSubmit(values: IncidentValues) {
    setServerError(null);
    const [kind, id] = values.subject.split(':');
    try {
      await report.mutateAsync({
        eventId,
        incidentType: values.incidentType,
        stockItemId: kind === 'item' ? (id ?? null) : null,
        assetId: kind === 'asset' ? (id ?? null) : null,
        quantity: values.quantity === '' ? null : Number(values.quantity),
        description: values.description.trim(),
        photo: photo?.file ?? null,
      });
      setSent(true);
    } catch (error) {
      const fieldErrors =
        isApiError(error) && error.status === 400 ? Object.entries(error.problem.errors ?? {}) : [];
      const mapped = fieldErrors.flatMap(([key, messages]) => {
        const field = formFieldFor[key];
        return field && messages[0] ? [{ field, message: messages[0] }] : [];
      });
      mapped.forEach(({ field, message }) => setError(field, { message }));
      //file storage names the field "file", whichever upload it's checking
      const photoMessage = fieldErrors.find(([key]) => key === 'file' || key === 'photo')?.[1][0];
      if (photoMessage) setPhotoError(photoMessage);
      if (mapped.length === 0 && !photoMessage) setServerError(describeReportError(error));
    }
  }

  function startAgain() {
    reset(blank);
    setPhoto(null);
    setSent(false);
  }

  if (sent) {
    return (
      <div className={styles.sent} role="status">
        <CircleCheck className={styles.sentIcon} aria-hidden="true" />
        <p className={styles.sentTitle}>Reported. Thanks, the office can see this now.</p>
        <div className={styles.buttons}>
          <Button onClick={startAgain}>Report something else</Button>
          <LinkButton to={doneTo} variant="primary">
            Done
          </LinkButton>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className={styles.form}>
      {listsBlocked && (
        <Alert tone="warning">
          The list of stock and equipment isn't open to your role yet, so a report can't be sent from here.
          Tell your crew lead what happened.
        </Alert>
      )}
      {listsFailed && (
        <Alert tone="danger">
          The list of stock and equipment didn't load.{' '}
          <Button
            variant="ghost"
            onClick={() => {
              void items.refetch();
              void equipment.refetch();
            }}
          >
            Try again
          </Button>
        </Alert>
      )}
      {serverError && <Alert tone="danger">{serverError}</Alert>}

      <fieldset className={styles.types} aria-describedby={errors.incidentType ? 'type-error' : undefined}>
        <legend className={styles.legend}>What kind of problem? (required)</legend>
        <div className={styles.typeGrid}>
          {incidentTypes.map((type) => (
            <label key={type} className={styles.type}>
              <input type="radio" value={type} {...register('incidentType')} />
              <span className={styles.typeName}>{incidentTypeLabels[type]}</span>
              <span className={styles.typeHint}>{typeHints[type]}</span>
            </label>
          ))}
        </div>
        {errors.incidentType && (
          <p id="type-error" className={styles.error}>
            {errors.incidentType.message}
          </p>
        )}
      </fieldset>

      <Field label="Item or equipment" required error={errors.subject?.message}>
        {(field) => (
          <select {...field} {...register('subject')} disabled={!items.data && !equipment.data}>
            <option value="">{items.isPending ? 'Loading…' : 'Choose what it was'}</option>
            {items.data && (
              <optgroup label="Stock">
                {items.data
                  .filter((item) => !item.isAsset)
                  .map((item) => (
                    <option key={item.stockItemId} value={`item:${item.stockItemId}`}>
                      {item.name}
                    </option>
                  ))}
              </optgroup>
            )}
            {equipment.data && equipment.data.length > 0 && (
              <optgroup label="Equipment with a serial number">
                {equipment.data.map((asset) => (
                  <option key={asset.assetId} value={`asset:${asset.assetId}`}>
                    {asset.stockItemName} {asset.serialNumber}
                  </option>
                ))}
              </optgroup>
            )}
          </select>
        )}
      </Field>

      <Field label="How many" hint="Leave it empty if it doesn't apply." error={errors.quantity?.message}>
        {(field) => <input {...field} {...register('quantity')} type="number" min={1} inputMode="numeric" />}
      </Field>

      <Field label="What happened" required error={errors.description?.message}>
        {(field) => (
          <textarea
            {...field}
            {...register('description')}
            rows={4}
            maxLength={2000}
            placeholder="What happened, and when"
          />
        )}
      </Field>

      <Field label="Photo" hint="Optional. Up to 10 MB." error={photoError ?? undefined}>
        {(field) => (
          <input {...field} type="file" accept="image/*" capture="environment" onChange={choosePhoto} />
        )}
      </Field>
      {photo && (
        <div className={styles.preview}>
          <img src={photo.preview} alt="Preview of what will be sent" />
          <Button variant="ghost" onClick={() => setPhoto(null)}>
            Remove photo
          </Button>
        </div>
      )}

      <Button type="submit" variant="primary" block busy={isSubmitting} disabled={listsBlocked}>
        {isSubmitting ? 'Sending…' : 'Send report'}
      </Button>
    </form>
  );
}
