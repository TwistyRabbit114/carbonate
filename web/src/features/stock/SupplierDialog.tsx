import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import type { Supplier } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { useSaveSupplier } from './api';
import form from '@/components/FormLayout.module.scss';

const supplierSchema = z.object({
  name: z.string().trim().min(1, 'Give the supplier a name.').max(200, 'Keep it under 200 characters.'),
  contactName: z.string().trim().max(200, 'Keep it under 200 characters.'),
  email: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || z.string().email().safeParse(value).success,
      'That email address looks wrong.',
    ),
  phone: z.string().trim().max(50, 'Keep it under 50 characters.'),
  leadTimeDays: z
    .string()
    .trim()
    .regex(/^\d{1,3}$/, 'Enter the lead time in whole days.'),
  isLiquorSupplier: z.boolean(),
  isActive: z.boolean(),
});

type SupplierValues = z.infer<typeof supplierSchema>;
type SupplierField = keyof SupplierValues;

const supplierFields = Object.keys(supplierSchema.shape) as SupplierField[];

type SupplierDialogProps = {
  supplier: Supplier | null; //null adds a new one
  onClose: () => void;
};

//who order lists go to (FR-28). the lead time drives the warning when an order would be too late (FR-29)
export function SupplierDialog({ supplier, onClose }: SupplierDialogProps) {
  const save = useSaveSupplier(supplier?.supplierId ?? null);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SupplierValues>({
    resolver: zodResolver(supplierSchema),
    defaultValues: {
      name: supplier?.name ?? '',
      contactName: supplier?.contactName ?? '',
      email: supplier?.email ?? '',
      phone: supplier?.phone ?? '',
      leadTimeDays: supplier ? String(supplier.leadTimeDays) : '',
      isLiquorSupplier: supplier?.isLiquorSupplier ?? false,
      isActive: supplier?.isActive ?? true,
    },
  });

  async function onSubmit(values: SupplierValues) {
    setServerError(null);
    try {
      await save.mutateAsync({
        name: values.name,
        contactName: values.contactName || null,
        email: values.email || null,
        phone: values.phone || null,
        leadTimeDays: Number(values.leadTimeDays),
        isLiquorSupplier: values.isLiquorSupplier,
        isActive: values.isActive,
      });
      toast.success(supplier ? `${values.name} is updated.` : `${values.name} is added.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        supplierFields.find((field) => field === key),
      );
      if (!landed)
        setServerError(
          problemMessage(error, "The supplier wasn't saved.", "Your role can't change the catalogue."),
        );
    }
  }

  return (
    <Dialog open wide title={supplier ? `Edit ${supplier.name}` : 'Add supplier'} onClose={onClose}>
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
          <Field
            label="Lead time"
            required
            hint="Days' notice they need before delivery."
            error={errors.leadTimeDays?.message}
          >
            {(field) => <input {...field} {...register('leadTimeDays')} inputMode="numeric" />}
          </Field>
          <Field label="Contact person" error={errors.contactName?.message}>
            {(field) => <input {...field} {...register('contactName')} maxLength={200} autoComplete="off" />}
          </Field>
          <Field label="Phone" error={errors.phone?.message}>
            {(field) => (
              <input {...field} {...register('phone')} type="tel" maxLength={50} autoComplete="off" />
            )}
          </Field>
          <Field label="Email" full error={errors.email?.message}>
            {(field) => <input {...field} {...register('email')} type="email" autoComplete="off" />}
          </Field>
          <fieldset className={`${form.checks} ${form.full}`}>
            <legend className={form.legend}>This supplier</legend>
            <label className={form.check}>
              <input type="checkbox" {...register('isLiquorSupplier')} />
              Supplies liquor
            </label>
            {supplier && (
              <label className={form.check}>
                <input type="checkbox" {...register('isActive')} />
                Is still used
              </label>
            )}
          </fieldset>
        </div>
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            {supplier ? 'Save changes' : 'Add supplier'}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
