import { ShieldX } from 'lucide-react';
import { Link } from 'react-router';
import { usePermissions } from '@/auth/AuthContext';
import { EmptyState } from '@/components/EmptyState';
import { PageHead } from '@/components/PageHead';
import { landingPath } from '../navigation';

//shown inside the shell when a route guard says no. it never shows any of the page's data
export function ForbiddenPage() {
  const check = usePermissions();

  return (
    <>
      <PageHead title="Not available" />
      <EmptyState title="This isn't available for your role" icon={ShieldX}>
        <p>
          <Link to={landingPath(check)}>Go to your start page</Link>
        </p>
      </EmptyState>
    </>
  );
}
