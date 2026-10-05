import type { ReactNode } from 'react';
import { usePermissions } from '@/auth/AuthContext';
import type { Permission } from '@/auth/permissions';
import { ForbiddenPage } from '../pages/ForbiddenPage';

type RequirePermissionProps = {
  anyOf: readonly Permission[];
  children: ReactNode;
};

//keeps people off screens their role can't use. the api is still the real gate
export function RequirePermission({ anyOf, children }: RequirePermissionProps) {
  const check = usePermissions();
  return check.canAny(anyOf) ? children : <ForbiddenPage />;
}
