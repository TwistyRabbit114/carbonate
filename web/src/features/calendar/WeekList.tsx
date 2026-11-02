import { Badge } from '@/components/Badge';
import { cx } from '@/lib/cx';
import { longDay } from './calendarDates';
import type { CalendarItem } from './items';
import { ItemList } from './ItemList';
import styles from './CalendarPage.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type WeekListProps = {
  week: string[];
  today: string;
  days: Map<string, CalendarItem[]>;
  linkTo: (eventId: string) => string;
};

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//monday to sunday down the page, which reads well on a phone and still works on a desk
export function WeekList({ week, today, days, linkTo }: WeekListProps) {
  return (
    <ol className={styles.week}>
      {week.map((date) => {
        const items = days.get(date) ?? [];
        return (
          <li key={date} className={cx(styles.weekDay, date === today && styles.isToday)}>
            <h2 className={styles.dayName} aria-current={date === today ? 'date' : undefined}>
              {longDay(date)} {date === today && <Badge tone="auto">Today</Badge>}
            </h2>
            {items.length > 0 ? (
              <ItemList items={items} linkTo={linkTo} />
            ) : (
              <p className={styles.quiet}>Nothing on.</p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
