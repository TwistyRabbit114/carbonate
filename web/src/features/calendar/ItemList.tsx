import { Link } from 'react-router';
import { cx } from '@/lib/cx';
import type { CalendarItem } from './items';
import styles from './CalendarPage.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type ItemListProps = {
  items: CalendarItem[];
  linkTo: (eventId: string) => string;
};

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//a day's events and milestones as full-width rows, each opening its event. used for the chosen
//day under the month and for every day of the week view
export function ItemList({ items, linkTo }: ItemListProps) {
  return (
    <ul className={styles.items}>
      {items.map((item) => (
        <li key={item.key}>
          <Link to={linkTo(item.eventId)} className={cx(styles.item, styles[item.kind])}>
            {/*the space keeps a screen reader from running "10:00" into the title*/}
            <span className={styles.time}>{item.time}</span> <span>{item.title}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
