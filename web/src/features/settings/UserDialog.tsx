import { useId, useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type UseFormRegisterReturn } from 'react-hook-form';
import { z } from 'zod';
import type { RoleName, UpdateUserRequest, UserListItem } from '@/api/types';
import { roleLabels } from '@/auth/roles';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { useToast } from '@/components/toast/ToastContext';
import { applyFieldErrors, problemMessage } from '@/lib/apiErrors';
import { useCreateUser, useUpdateUser } from './api';
import form from '@/components/FormLayout.module.scss';

//----------------------------------------------------------\\
//                              FORM
//----------------------------------------------------------\\

export const roleOrder: RoleName[] = [
  'Director',
  'OperationsManager',
  'EventManager',
  'Accounts',
  'CrewLead',
  'CasualCrew',
];

const rolesField = z.array(z.enum(roleOrder)).min(1, 'Choose at least one role.');

//the api's rules for a new account (FR-38). its password check also turns away breached ones
const newUserSchema = z.object({
  fullName: z.string().trim().min(1, 'Enter their name.').max(200, 'Keep it under 200 characters.'),
  email: z.string().trim().min(1, 'Enter their email address.').email('That email address looks wrong.'),
  employeeNumber: z
    .string()
    .trim()
    .min(1, 'Enter their employee number.')
    .max(30, 'Keep it under 30 characters.')
    .regex(/^[A-Za-z0-9-]+$/, 'Use letters, numbers and hyphens only.'),
  employmentType: z.enum(['Permanent', 'Casual']),
  roles: rolesField,
  initialPassword: z
    .string()
    .min(12, 'Use at least 12 characters.')
    .max(128, 'Keep it under 128 characters.'),
});

const editUserSchema = z.object({
  fullName: z.string().trim().min(1, 'Enter their name.').max(200, 'Keep it under 200 characters.'),
  roles: rolesField,
  isActive: z.boolean(),
});

type NewUserValues = z.infer<typeof newUserSchema>;
type EditUserValues = z.infer<typeof editUserSchema>;

//----------------------------------------------------------\\
//                              ROLE CHOICES
//----------------------------------------------------------\\

type RoleChoicesProps = {
  registration: UseFormRegisterReturn<'roles'>; //one registration across the boxes gives an array
  error?: string;
};

//several roles are fine, the account gets everything each one allows
function RoleChoices({ registration, error }: RoleChoicesProps) {
  const hintId = useId();

  return (
    <fieldset className={`${form.checks} ${form.full}`} aria-describedby={hintId}>
      <legend className={form.legend}>Roles (required)</legend>
      {roleOrder.map((role) => (
        <label key={role} className={form.check}>
          <input type="checkbox" value={role} {...registration} />
          {roleLabels[role]}
        </label>
      ))}
      <p id={hintId} className={form.hint}>
        Only the Director can give someone the Director role.
      </p>
      {error && <p className={form.error}>{error}</p>}
    </fieldset>
  );
}

//----------------------------------------------------------\\
//                              NEW USER
//----------------------------------------------------------\\

const newUserFields: (keyof NewUserValues)[] = [
  'fullName',
  'email',
  'employeeNumber',
  'employmentType',
  'roles',
  'initialPassword',
];

export function NewUserDialog({ onClose }: { onClose: () => void }) {
  const create = useCreateUser();
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<NewUserValues>({
    resolver: zodResolver(newUserSchema),
    defaultValues: {
      fullName: '',
      email: '',
      employeeNumber: '',
      employmentType: 'Permanent',
      roles: [],
      initialPassword: '',
    },
  });

  async function onSubmit(values: NewUserValues) {
    setServerError(null);
    try {
      const user = await create.mutateAsync(values);
      toast.success(`${user.fullName} can sign in now.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) => newUserFields.find((field) => field === key));
      if (!landed) setServerError(problemMessage(error, "The account wasn't created."));
    }
  }

  return (
    <Dialog open wide title="Add user" onClose={onClose}>
      <p className={form.lead}>
        Give them the password yourself, they can sign in with it straight away. Directors and Accounts set up
        a two-step code the first time.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Name" required error={errors.fullName?.message}>
            {(field) => <input {...field} {...register('fullName')} maxLength={200} autoComplete="off" />}
          </Field>
          <Field label="Email" required error={errors.email?.message}>
            {(field) => <input {...field} {...register('email')} type="email" autoComplete="off" />}
          </Field>
          <Field label="Employee number" required error={errors.employeeNumber?.message}>
            {(field) => (
              <input {...field} {...register('employeeNumber')} maxLength={30} autoComplete="off" />
            )}
          </Field>
          <Field
            label="Employment"
            hint="Casual crew are switched off automatically a week after their last event's debrief."
            error={errors.employmentType?.message}
          >
            {(field) => (
              <select {...field} {...register('employmentType')}>
                <option value="Permanent">Permanent</option>
                <option value="Casual">Casual</option>
              </select>
            )}
          </Field>
          <RoleChoices registration={register('roles')} error={errors.roles?.message} />
          <Field
            label="First password"
            required
            hint="At least 12 characters. Carbonate refuses passwords that have been in a data breach."
            error={errors.initialPassword?.message}
          >
            {(field) => (
              <input
                {...field}
                {...register('initialPassword')}
                type="password"
                autoComplete="new-password"
              />
            )}
          </Field>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Add user
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}

//----------------------------------------------------------\\
//                              EDIT USER
//----------------------------------------------------------\\

const sameRoles = (a: readonly RoleName[], b: readonly RoleName[]) =>
  a.length === b.length && a.every((role) => b.includes(role));

//only what changed is sent. the api turns away roles sent for your own account even when
//they're the same, and the last active director can't be switched off or lose the role
function changesFrom(user: UserListItem, values: EditUserValues): UpdateUserRequest {
  const change: UpdateUserRequest = {};
  if (values.fullName !== user.fullName) change.fullName = values.fullName;
  if (!sameRoles(values.roles, user.roles)) change.roles = values.roles;
  if (values.isActive !== user.isActive) change.isActive = values.isActive;
  return change;
}

export function EditUserDialog({ user, onClose }: { user: UserListItem; onClose: () => void }) {
  const update = useUpdateUser(user.userId);
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<EditUserValues>({
    resolver: zodResolver(editUserSchema),
    defaultValues: { fullName: user.fullName, roles: user.roles, isActive: user.isActive },
  });

  async function onSubmit(values: EditUserValues) {
    setServerError(null);
    const change = changesFrom(user, values);
    if (Object.keys(change).length === 0) return onClose();
    try {
      await update.mutateAsync(change);
      toast.success(`${values.fullName}'s account is updated.`);
      onClose();
    } catch (error) {
      const landed = applyFieldErrors(error, setError, (key) =>
        key === 'fullName' || key === 'roles' ? key : undefined,
      );
      if (!landed) setServerError(problemMessage(error, "The account wasn't changed."));
    }
  }

  return (
    <Dialog open wide title={`Edit ${user.fullName}`} onClose={onClose}>
      <p className={form.lead}>
        {user.email}, employee number {user.employeeNumber}. A change of role or access takes effect within 15
        minutes, the next time their sign-in refreshes.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className={form.fields}>
          <Field label="Name" required full error={errors.fullName?.message}>
            {(field) => <input {...field} {...register('fullName')} maxLength={200} autoComplete="off" />}
          </Field>
          <RoleChoices registration={register('roles')} error={errors.roles?.message} />
          <fieldset className={`${form.checks} ${form.full}`}>
            <legend className={form.legend}>Access</legend>
            <label className={form.check}>
              <input type="checkbox" {...register('isActive')} />
              Can sign in
            </label>
          </fieldset>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={isSubmitting}>
            Save changes
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
