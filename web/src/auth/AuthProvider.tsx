import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { apiFetch, refreshSession, setAccessToken, setSessionExpiredHandler } from '@/api/client';
import type { MeResponse } from '@/api/types';
import { AuthContext, type AuthState } from './AuthContext';

type AuthProviderProps = {
  children: ReactNode;
};

export function AuthProvider({ children }: AuthProviderProps) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<AuthState>({ status: 'loading' });

  //----------------------------------------------------------\\
  //                              RESTORE
  //----------------------------------------------------------\\

  useEffect(() => {
    let active = true;

    //a request that can't refresh means the session is over, drop everything cached for this user
    setSessionExpiredHandler(() => {
      queryClient.clear();
      setState({ status: 'signedOut', reason: 'expired' });
    });

    //a reload loses the in-memory token, so ask the refresh cookie for a new one.
    //strict mode runs this twice in dev, the single-flight refresh keeps it to one request
    void (async () => {
      const refreshed = await refreshSession();
      if (!active) return;
      if (!refreshed) {
        setState({ status: 'signedOut' });
        return;
      }

      try {
        const me = await apiFetch<MeResponse>('/me');
        if (active) setState({ status: 'signedIn', me });
      } catch {
        if (active) setState({ status: 'signedOut' });
      }
    })();

    return () => {
      active = false;
      setSessionExpiredHandler(null);
    };
  }, [queryClient]);

  //----------------------------------------------------------\\
  //                              LOGOUT
  //----------------------------------------------------------\\

  const logout = useCallback(async () => {
    try {
      await apiFetch('/auth/logout', { method: 'POST' });
    } catch {
      //the server may have dropped the session already, signing out here is what matters
    }

    //clear the cache so the next person on this device never sees this user's data
    setAccessToken(null);
    queryClient.clear();
    setState({ status: 'signedOut', reason: 'loggedOut' });
  }, [queryClient]);

  const value = useMemo(() => ({ state, logout }), [state, logout]);

  return <AuthContext value={value}>{children}</AuthContext>;
}
