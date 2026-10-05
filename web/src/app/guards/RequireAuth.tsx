import { Navigate, Outlet, useLocation } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { SessionLoading } from '../pages/SessionLoading';

//no session sends the user to login, remembering where they were going.
//convenience only, the api checks every request regardless
export function RequireAuth() {
  const { state } = useAuth();
  const location = useLocation();

  if (state.status === 'loading') return <SessionLoading />;

  if (state.status === 'signedOut') {
    const params = new URLSearchParams();
    const next = location.pathname + location.search;

    //after a deliberate logout the next person starts fresh, not on the last user's page
    if (state.reason !== 'loggedOut' && next !== '/') params.set('next', next);
    if (state.reason === 'expired') params.set('reason', 'expired');

    const query = params.toString();
    return <Navigate to={query ? `/login?${query}` : '/login'} replace />;
  }

  return <Outlet />;
}
