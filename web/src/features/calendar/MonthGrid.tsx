import { useEffect, useRef, type KeyboardEvent } from 'react';
import { Link } from 'react-router';
import { cx } from '@/lib/cx';
import { addDays, dayNumber, longDay, monthTitle, sameMonth, startOfWeek, weekdays } from './calendarDates';
import type { CalendarItem } from './items';
import styles from './CalendarPage.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type MonthGridProps = {
  weeks: string[][];
  month: string; //any day in the month being shown
  today: string;
  selected: string;
  days: Map<string, CalendarItem[]>;
  linkTo: (eventId: string) => string;
  onSelect: (date: string, fromKeyboard?: boolean) => void;
  onMore: (date: string) => void;
  focusKey: number; //goes up when a key press picks a day, so focus follows it
};

//how many 24px chips fit in a cell before "+3 more"
const shown = 2;

//arrow keys walk the days, home and end jump to the ends of the week
const moves: Record<string, (date: string) => string> = {
  ArrowLeft: (date) => addDays(date, -1),
  ArrowRight: (date) => addDays(date, 1),
  ArrowUp: (date) => addDays(date, -7),
  ArrowDown: (date) => addDays(date, 7),
  Home: (date) => startOfWeek(date),
  End: (date) => addDays(startOfWeek(date), 6),
};

const plural = (count: number) => (count === 1 ? '1 item' : `${count} items`);

//a cell only has room for two, so the event itself goes first and its early milestones (load-in,
//rehearsal) can't push it out. sort keeps the time order within each kind
const eventsFirst = (items: CalendarItem[]) =>
  [...items].sort((a, b) => (a.kind === b.kind ? 0 : a.kind === 'event' ? -1 : 1));

//----------------------------------------------------------\\
//                              GRID
//----------------------------------------------------------\\

//a real table: weekdays are column heads, each day a cell. only the chosen day's button is in the
//tab order, the arrow keys move between days, and the chips are links to their events
export function MonthGrid({
  weeks,
  month,
  today,
  selected,
  days,
  linkTo,
  onSelect,
  onMore,
  focusKey,
}: MonthGridProps) {
  const buttons = useRef(new Map<string, HTMLButtonElement>());

  //after a key press the chosen day may be in a grid that has only just rendered
  useEffect(() => {
    if (focusKey > 0) buttons.current.get(selected)?.focus();
  }, [focusKey, selected]);

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>, date: string) {
    const move = moves[event.key];
    if (!move) return;
    event.preventDefault();
    onSelect(move(date), true);
  }

  return (
    <table className={styles.grid}>
      <caption className="visually-hidden">{monthTitle(month)}</caption>
      <thead>
        <tr>
          {weekdays.map((weekday) => (
            <th key={weekday.name} scope="col">
              <span aria-hidden="true">{weekday.short}</span>
              <span className="visually-hidden">{weekday.name}</span>
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {weeks.map((week) => (
          <tr key={week[0]}>
            {week.map((date) => {
              const items = days.get(date) ?? [];
              return (
                <td
                  key={date}
                  className={cx(
                    styles.cell,
                    !sameMonth(date, month) && styles.outside,
                    date === selected && styles.selected,
                  )}
                >
                  <button
                    type="button"
                    ref={(node) => {
                      if (node) buttons.current.set(date, node);
                      else buttons.current.delete(date);
                    }}
                    className={cx(styles.day, date === today && styles.today)}
                    tabIndex={date === selected ? 0 : -1}
                    aria-pressed={date === selected}
                    aria-current={date === today ? 'date' : undefined}
                    aria-label={items.length ? `${longDay(date)}, ${plural(items.length)}` : longDay(date)}
                    onClick={() => onSelect(date)}
                    onKeyDown={(event) => onKeyDown(event, date)}
                  >
                    {dayNumber(date)}
                  </button>
                  {items.length > 0 && (
                    <>
                      <span className={styles.count} aria-hidden="true">
                        {items.length}
                      </span>
                      <ul className={styles.chips}>
                        {eventsFirst(items)
                          .slice(0, shown)
                          .map((item) => (
                            <li key={item.key}>
                              <Link to={linkTo(item.eventId)} className={cx(styles.chip, styles[item.kind])}>
                                {item.title}
                              </Link>
                            </li>
                          ))}
                      </ul>
                      {items.length > shown && (
                        <button
                          type="button"
                          className={styles.more}
                          aria-label={`${items.length - shown} more on ${longDay(date)}`}
                          onClick={() => onMore(date)}
                        >
                          +{items.length - shown} more
                        </button>
                      )}
                    </>
                  )}
                </td>
              );
            })}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
