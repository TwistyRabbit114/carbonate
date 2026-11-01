import { useMutation } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';

//----------------------------------------------------------\\
//                              CHANGE PASSWORD
//----------------------------------------------------------\\

//TODO(plan): POST /api/auth/password isn't in the contract yet, so the body is typed here. asked of C:
//204 when it's changed, a 400 keyed by field for a wrong current password or one the policy turns away
//(never a 401, that reads as a dead session), rate limited like login, audited, and other devices
//signed out. it sits under /api/auth so the refresh cookie comes along and this device can stay in
export type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
};

export function useChangePassword() {
  return useMutation({
    mutationFn: (change: ChangePasswordRequest) =>
      apiFetch<void>('/auth/password', { method: 'POST', json: change }),
  });
}
