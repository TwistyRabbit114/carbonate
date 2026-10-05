import { usePermissions } from '@/auth/AuthContext';
import { ErrorState } from '@/components/ErrorState';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useAdminBoard } from '@/features/admin-tasks/api';
import { useMyEvents } from '@/features/events/api';
import { formatDateOnly } from '@/lib/format';
import { crewSchedule } from './schedule';
import { AdminTaskList, EventTaskList } from './TaskLists';
import styles from './MyTasksPage.module.scss';

//everything assigned to me, event by event, then the general admin tasks (FR-19, FR-21)
export default function MyTasksPage() {
  const check = usePermissions();
  const events = useMyEvents();
  const seesAdmin = check.can('admin_task.view');
  const admin = useAdminBoard(seesAdmin);

  if (events.isError) {
    return (
      <>
        <PageHead title="My tasks" />
        <ErrorState message="We couldn't load your tasks." onRetry={() => void events.refetch()} />
      </>
    );
  }
  if (!events.data) return <PageSkeleton />;

  //finished events' cards are history, so only events still to come or on now
  const { next, upcoming } = crewSchedule(events.data);
  const current = [next, ...upcoming].filter((event) => event !== undefined);

  return (
    <>
      <PageHead title="My tasks" />

      {current.map((event) => (
        <section key={event.eventId} className={styles.group} aria-labelledby={`tasks-${event.eventId}`}>
          <h2 id={`tasks-${event.eventId}`} className={styles.heading}>
            {event.name}
            <span className={styles.when}>{formatDateOnly(event.eventDate)}</span>
          </h2>
          <EventTaskList eventId={event.eventId} empty="Nothing assigned to you on this event yet." />
        </section>
      ))}

      {seesAdmin && (
        <section className={styles.group} aria-labelledby="general-tasks">
          <h2 id="general-tasks" className={styles.heading}>
            General tasks
          </h2>
          {admin.isError ? (
            <ErrorState message="We couldn't load your general tasks." onRetry={() => void admin.refetch()} />
          ) : admin.data ? (
            <AdminTaskList board={admin.data} empty="No general tasks for you right now." />
          ) : (
            <p role="status">Loading general tasks…</p>
          )}
        </section>
      )}

      {current.length === 0 && !seesAdmin && <p className={styles.empty}>No tasks for you right now.</p>}
    </>
  );
}
