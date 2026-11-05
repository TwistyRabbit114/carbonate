import { useMutation } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import type { ChangePasswordRequest } from '@/api/types';

//----------------------------------------------------------\\
//                              CHANGE PASSWORD
//----------------------------------------------------------\\

//204 when it's changed. a wrong current password or one the policy turns away is a 400 on that field,
//never a 401. it sits under /api/auth so the refresh cookie comes along and this device stays in
export function useChangePassword() {
  return useMutation({
    mutationFn: (change: ChangePasswordRequest) =>
      apiFetch<void>('/auth/password', { method: 'POST', json: change }),
  });
}
