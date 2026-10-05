import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { EquipmentAsset, StockItem } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { useSaveEquipment } from './api';
import form from '@/components/FormLayout.module.scss';

const assetSchema = z.object({
  stockItemId: z.string().min(1, 'Choose what kind of equipment it is.'),
  serialNumber: z
    .string()
    .trim()
    .min(1, 'Give the asset a serial number.')
    .max(100, 'Keep it under 100 characters.'),
  condition: z.string().trim().min(1, 'Say what condition it is in.').max(50, 'Keep it under 50 characters.'),
  status: z
    .string()
    .trim()
    .min(1, 'Say where it is, like Available or Out for repair.')
    .max(50, 'Keep it under 50 characters.'),
  purchaseDate: z.string(),
});

type AssetValues = z.infer<typeof assetSchema>;
type AssetField = keyof AssetValues;

const assetFields = Object.keys(assetSchema.shape) as AssetField[];

type EquipmentDialogProps = {
  asset: EquipmentAsset | null; //null adds a new one
  kinds: StockItem[]; //catalogue items that are serialised equipment
  onClose: () => void;
};

//one serialised piece of kit, like an ice machine, so incidents can name the exact one (FR-31)
export function EquipmentDialog({ asset, kinds, onClose }: EquipmentDialogProps) {
  const save = useSaveEquipment(asset?.assetId ?? null);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<AssetValues>({
    resolver: zodResolver(assetSchema),
    defaultValues: {
      stockItemId: asset?.stockItemId ?? '',
      serialNumber: asset?.serialNumber ?? '',
      condition: asset?.condition ?? 'Good',
      status: asset?.status ?? 'Available',
      purchaseDate: asset?.purchaseDate ?? '',
    },
  });

  async function onSubmit(values: AssetValues) {
    setServerError(null);
    try {
      await save.mutateAsync({ ...values, purchaseDate: values.purchaseDate || null });
      toast.success(asset ? `${values.serialNumber} is updated.` : `${values.serialNumber} is recorded.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => assetFields.find((field) => field === key));
      if (!landed)
        setServerError(
          problemMessage(error, "The equipment wasn't saved.", "Your role can't change the catalogue."),
        );
    }
  }

  return (
    <Dialog open wide title={asset ? `Edit ${asset.serialNumber}` : 'Add equipment'} onClose={onClose}>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Kind" required error={errors.stockItemId?.message}>
            {(field) => (
              <select {...field} {...register('stockItemId')}>
                <option value="">Choose what it is</option>
                {kinds.map((kind) => (
                  <option key={kind.stockItemId} value={kind.stockItemId}>
                    {kind.name}
                  </option>
                ))}
              </select>
            )}
          </Field>
          <Field label="Serial number" required error={errors.serialNumber?.message}>
            {(field) => <input {...field} {...register('serialNumber')} maxLength={100} autoComplete="off" />}
          </Field>
          <Field label="Condition" required error={errors.condition?.message}>
            {(field) => <input {...field} {...register('condition')} maxLength={50} autoComplete="off" />}
          </Field>
          <Field label="Status" required error={errors.status?.message}>
            {(field) => <input {...field} {...register('status')} maxLength={50} autoComplete="off" />}
          </Field>
          <Field label="Bought on" error={errors.purchaseDate?.message}>
            {(field) => <input {...field} {...register('purchaseDate')} type="date" />}
          </Field>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            {asset ? 'Save changes' : 'Add equipment'}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
