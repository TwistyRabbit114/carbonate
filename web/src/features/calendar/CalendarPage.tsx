import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { usePermissions } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { PageHead } from '@/components/PageHead';
import { phoneQuery, useMediaQuery } from '@/lib/useMediaQuery';
import { useCalendarItems } from './api';
import {
  addDays,
  addMonths,
  isCalendarDate,
  longDay,
  monthTitle,
  monthWeeks,
  todayInSast,
  weekOf,
  weekTitle,
} from './calendarDates';
import { ItemList } from './ItemList';
import { MonthGrid } from './MonthGrid';
import { WeekList } from './WeekList';
import styles from './CalendarPage.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type View = 'month' | 'week';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//Carbonate's own events and milestones on a month or week grid (FR-39). the view and the chosen
//day live in the address, so a link or a refresh lands on the same place. month is the default on
//a desk, week on a phone
export default function CalendarPage() {
  const [params, setParams] = useSearchParams();
  const phone = useMediaQuery(phoneQuery);
  const check = usePermissions();
  const today = todayInSast();
  const [focusKey, setFocusKey] = useState(0);
  const [agendaKey, setAgendaKey] = useState(0);
  const agenda = useRef<HTMLHeadingElement>(null);

  const asked = params.get('view');
  const view: View = asked === 'week' || asked === 'month' ? asked : phone ? 'week' : 'month';
  const date = isCalendarDate(params.get('date')) ? params.get('date')! : today;
  const weeks = view === 'month' ? monthWeeks(date) : [weekOf(date)];
  const shownDays = weeks.flat();
  const calendar = useCalendarItems(shownDays[0] ?? date, shownDays.at(-1) ?? date);

  //crew open their events through My events, everyone else through the events board
  const linkTo = (eventId: string) =>
    check.can('event.view_all') ? `/events/${eventId}` : `/my/events/${eventId}`;

  //"+2 more" moves focus to the day's full list below the grid
  useEffect(() => {
    if (agendaKey > 0) agenda.current?.focus();
  }, [agendaKey]);

  //paging and switching views are steps worth going back to, picking a day isn't
  const go = (next: { view?: View; date?: string }, replace = false) =>
    setParams({ view: next.view ?? view, date: next.date ?? date }, { replace });
  const step = (by: number) => go({ date: view === 'month' ? addMonths(date, by) : addDays(date, 7 * by) });
  const select = (day: string, fromKeyboard = false) => {
    go({ date: day }, true);
    if (fromKeyboard) setFocusKey((key) => key + 1);
  };

  const empty = !calendar.isPending && shownDays.every((day) => !calendar.days.has(day));

  return (
    <>
      <PageHead
        title={view === 'month' ? monthTitle(date) : weekTitle(date)}
        eyebrow="Carbonate's own data, not the Google Calendar sync"
        actions={
          <div className={styles.toolbar}>
            <div className={styles.views} role="group" aria-label="View">
              {(['month', 'week'] as const).map((option) => (
                <Button
                  key={option}
                  variant={view === option ? 'primary' : 'default'}
                  aria-pressed={view === option}
                  onClick={() => go({ view: option })}
                >
                  {option === 'month' ? 'Month' : 'Week'}
                </Button>
              ))}
            </div>
            <div className={styles.steps}>
              <Button variant="ghost" aria-label={`Previous ${view}`} onClick={() => step(-1)}>
                <ChevronLeft aria-hidden="true" />
              </Button>
              <Button onClick={() => go({ date: today })}>Today</Button>
              <Button variant="ghost" aria-label={`Next ${view}`} onClick={() => step(1)}>
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </div>
        }
      />

      {calendar.isError ? (
        <ErrorState message="We couldn't load the calendar." onRetry={calendar.refetch} />
      ) : (
        <div aria-busy={calendar.isPending}>
          {calendar.isPending && (
            <p className={styles.quiet} role="status">
              Loading the calendar…
            </p>
          )}
          {calendar.milestonesFailed && (
            <div className={styles.alert}>
              <Alert tone="warning">
                Some milestones didn't load, so only their events are showing.{' '}
                <Button variant="ghost" onClick={calendar.retryMilestones}>
                  Try again
                </Button>
              </Alert>
            </div>
          )}
          {empty && <p className={styles.quiet}>Nothing on the calendar this {view} yet.</p>}

          {view === 'month' ? (
            <>
              <MonthGrid
                weeks={weeks}
                month={date}
                today={today}
                selected={date}
                days={calendar.days}
                linkTo={linkTo}
                onSelect={select}
                onMore={(day) => {
                  select(day);
                  setAgendaKey((key) => key + 1);
                }}
                focusKey={focusKey}
              />
              <section className={styles.agenda} aria-labelledby="calendar-day">
                <h2 id="calendar-day" ref={agenda} tabIndex={-1} className={styles.dayName}>
                  {longDay(date)}
                </h2>
                {calendar.days.get(date)?.length ? (
                  <ItemList items={calendar.days.get(date)!} linkTo={linkTo} />
                ) : (
                  <p className={styles.quiet}>Nothing on this day.</p>
                )}
              </section>
            </>
          ) : (
            <WeekList week={shownDays} today={today} days={calendar.days} linkTo={linkTo} />
          )}
        </div>
      )}
    </>
  );
}
