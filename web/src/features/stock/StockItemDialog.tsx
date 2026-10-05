import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { SaveStockItemRequest, StockItem } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { parseRand } from '@/lib/format';
import { useSaveStockItem, useStockCategories, useSuppliers } from './api';
import form from '@/components/FormLayout.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

//a number box that may be left empty, read with a comma or a point
const optionalNumber = z
  .string()
  .trim()
  .refine(
    (value) =>
      value === '' ||
      (Number.isFinite(Number(value.replace(',', '.'))) && Number(value.replace(',', '.')) >= 0),
    'Enter a number of 0 or more.',
  );

const numberOrNull = (value: string) => (value.trim() === '' ? null : Number(value.trim().replace(',', '.')));

const itemSchema = z.object({
  name: z.string().trim().min(1, 'Give the item a name.').max(200, 'Keep it under 200 characters.'),
  sku: z.string().trim().min(1, 'Give the item a SKU.').max(50, 'Keep it under 50 characters.'),
  categoryId: z.string().min(1, 'Choose a category.'),
  unit: z
    .string()
    .trim()
    .min(1, 'Say what it is counted in, like kg or bottles.')
    .max(30, 'Keep it under 30 characters.'),
  defaultSupplierId: z.string(),
  consumptionPerHundredGuests: optionalNumber,
  reorderLevel: optionalNumber,
  standardUnitCost: z
    .string()
    .trim()
    .refine((value) => value === '' || parseRand(value) !== null, 'Enter the cost in rand, like 4,20.'),
  isConsumable: z.boolean(),
  isAsset: z.boolean(),
  isActive: z.boolean(),
});

type ItemValues = z.infer<typeof itemSchema>;
type ItemField = keyof ItemValues;

const itemFields = Object.keys(itemSchema.shape) as ItemField[];

function valuesFrom(item: StockItem | null): ItemValues {
  const text = (value: number | null | undefined) => (value == null ? '' : String(value));
  return {
    name: item?.name ?? '',
    sku: item?.sku ?? '',
    categoryId: item?.categoryId ?? '',
    unit: item?.unit ?? '',
    defaultSupplierId: item?.defaultSupplierId ?? '',
    consumptionPerHundredGuests: text(item?.consumptionPerHundredGuests),
    reorderLevel: text(item?.reorderLevel),
    //rand are written with a decimal comma
    standardUnitCost: text(item?.standardUnitCost).replace('.', ','),
    isConsumable: item?.isConsumable ?? true,
    isAsset: item?.isAsset ?? false,
    isActive: item?.isActive ?? true,
  };
}

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

type StockItemDialogProps = {
  item: StockItem | null; //null adds a new one
  onClose: () => void;
};

//the catalogue by category and division (FR-24). consumption per 100 guests drives the quantities
//new events start with and the shortfall warning (FR-25, FR-27). the standard cost is $cost: only
//someone who can see it gets the box, and the api leaves it alone for anyone else
export function StockItemDialog({ item, onClose }: StockItemDialogProps) {
  const canCost = usePermissions().can('finance.view_internal_cost');
  const categories = useStockCategories();
  const suppliers = useSuppliers();
  const save = useSaveStockItem(item?.stockItemId ?? null);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ItemValues>({ resolver: zodResolver(itemSchema), defaultValues: valuesFrom(item) });

  async function onSubmit(values: ItemValues) {
    setServerError(null);
    const request: SaveStockItemRequest = {
      name: values.name,
      sku: values.sku,
      categoryId: values.categoryId,
      unit: values.unit,
      defaultSupplierId: values.defaultSupplierId || null,
      consumptionPerHundredGuests: numberOrNull(values.consumptionPerHundredGuests),
      reorderLevel: numberOrNull(values.reorderLevel),
      standardUnitCost: canCost && values.standardUnitCost ? parseRand(values.standardUnitCost) : null,
      isConsumable: values.isConsumable,
      isAsset: values.isAsset,
      isActive: values.isActive,
    };
    try {
      await save.mutateAsync(request);
      toast.success(item ? `${values.name} is updated.` : `${values.name} is in the catalogue.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => itemFields.find((field) => field === key));
      if (!landed)
        setServerError(
          problemMessage(error, "The item wasn't saved.", "Your role can't change the catalogue."),
        );
    }
  }

  return (
    <Dialog open wide title={item ? `Edit ${item.name}` : 'Add stock item'} onClose={onClose}>
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
          <Field label="SKU" required error={errors.sku?.message}>
            {(field) => <input {...field} {...register('sku')} maxLength={50} autoComplete="off" />}
          </Field>
          <Field label="Category" required error={errors.categoryId?.message}>
            {(field) => (
              <select {...field} {...register('categoryId')}>
                <option value="">Choose a category</option>
                {categories.data?.map((category) => (
                  <option key={category.categoryId} value={category.categoryId}>
                    {category.name}
                  </option>
                ))}
              </select>
            )}
          </Field>
          <Field label="Counted in" required hint="Like kg, bottles or units." error={errors.unit?.message}>
            {(field) => <input {...field} {...register('unit')} maxLength={30} autoComplete="off" />}
          </Field>
          <Field
            label="Supplier"
            hint="Lines to order or rent go on this supplier's list."
            error={errors.defaultSupplierId?.message}
          >
            {(field) => (
              <select {...field} {...register('defaultSupplierId')}>
                <option value="">No supplier, we hold it</option>
                {suppliers.data?.map((supplier) => (
                  <option key={supplier.supplierId} value={supplier.supplierId}>
                    {supplier.name}
                  </option>
                ))}
              </select>
            )}
          </Field>
          <Field
            label="Used per 100 guests"
            hint="Sets the starting quantity on new events and the shortfall warning."
            error={errors.consumptionPerHundredGuests?.message}
          >
            {(field) => <input {...field} {...register('consumptionPerHundredGuests')} inputMode="decimal" />}
          </Field>
          <Field label="Reorder level" error={errors.reorderLevel?.message}>
            {(field) => <input {...field} {...register('reorderLevel')} inputMode="decimal" />}
          </Field>
          {canCost && (
            <Field label="Standard cost" hint="In rand, per unit." error={errors.standardUnitCost?.message}>
              {(field) => (
                <input {...field} {...register('standardUnitCost')} inputMode="decimal" autoComplete="off" />
              )}
            </Field>
          )}
          <fieldset className={`${form.checks} ${form.full}`}>
            <legend className={form.legend}>This item</legend>
            <label className={form.check}>
              <input type="checkbox" {...register('isConsumable')} />
              Gets used up
            </label>
            <label className={form.check}>
              <input type="checkbox" {...register('isAsset')} />
              Is equipment with serial numbers
            </label>
            {item && (
              <label className={form.check}>
                <input type="checkbox" {...register('isActive')} />
                Is still in use
              </label>
            )}
          </fieldset>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            {item ? 'Save changes' : 'Add item'}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
