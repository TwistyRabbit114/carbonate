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

export type AuthContextValue = {
  state: AuthState;
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
