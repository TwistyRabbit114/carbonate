import { createContext, useContext, useMemo } from 'react';
import type { MeResponse } from '@/api/types';
import { permissionCheck } from './permissions';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

export type AuthState =
  | { status: 'loading' }
  | { status: 'signedOut'; reason?: 'expired' | 'loggedOut' }
  | { status: 'signedIn'; me: MeResponse };

//the short-lived token between the password and the mfa code. memory only, a reload
//drops it and the user starts again, which is what we want
export type PendingMfa = {
  mfaToken: string;
};

export type AuthContextValue = {
  state: AuthState;
  pendingMfa: PendingMfa | null;
  beginMfa: (pending: PendingMfa) => void;
  completeLogin: (accessToken: string) => Promise<MeResponse>;
  logout: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextValue | null>(null);

//----------------------------------------------------------\\
//                              HOOKS
//----------------------------------------------------------\\

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth needs to be inside AuthProvider');
  return value;
}

//for anything rendered behind RequireAuth, where a signed-in user is guaranteed
export function useSignedIn() {
  const { state } = useAuth();
  if (state.status !== 'signedIn') throw new Error('useSignedIn needs to be behind RequireAuth');
  return state.me;
}

export function usePermissions() {
  const me = useSignedIn();
  return useMemo(() => permissionCheck(me.permissions), [me.permissions]);
}
