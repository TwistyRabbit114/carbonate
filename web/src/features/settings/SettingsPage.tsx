import { usePermissions } from '@/auth/AuthContext';
import { PageHead } from '@/components/PageHead';
import { AuditPanel } from './AuditPanel';
import { CalendarPanel } from './CalendarPanel';
import { UsersPanel } from './UsersPanel';
import { VenuesPanel } from './VenuesPanel';

//the administration screen, one panel per job, each only for the permission it needs. the
//director gets all four, ops everything but the audit log, an event manager just venues
export default function SettingsPage() {
  const check = usePermissions();

  return (
    <>
      <PageHead title="Settings" eyebrow="Administration" />
      {check.can('user.manage') && <UsersPanel />}
      {check.can('calendar.connect') && <CalendarPanel />}
      {check.can('venue.edit') && <VenuesPanel />}
      {check.can('audit.view') && <AuditPanel />}
    </>
  );
}
