import { useId, useState, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import { cx } from '@/lib/cx';
import { phoneQuery, useMediaQuery } from '@/lib/useMediaQuery';
import styles from './Kanban.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

//how a column looks while a card is being dragged. none of these are set when nothing is
export type DropState = 'checking' | 'allowed' | 'over' | 'blocked';

type KanbanColumnProps = {
  title: string;
  count: number;
  badge?: ReactNode; //e.g. the "Auto" badge on columns the server moves events into
  emptyText?: string;
  startsOpenOnPhone?: boolean;
  columnRef?: (element: HTMLElement | null) => void; //lets drag and drop treat the column as a target
  dropState?: DropState;
  children: ReactNode; //the <li> cards
};

const dropHints: Record<DropState, string> = {
  checking: 'Checking…',
  allowed: 'Drop here to move',
  over: 'Drop here to move',
  blocked: "Can't move here",
};

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//desktop shows every column side by side. on phones they stack, and each heading folds its
//column away so the one that matters can sit at the top
export function KanbanColumn({
  title,
  count,
  badge,
  emptyText = 'Nothing here',
  startsOpenOnPhone = true,
  columnRef,
  dropState,
  children,
}: KanbanColumnProps) {
  const titleId = useId();
  const bodyId = useId();
  const isPhone = useMediaQuery(phoneQuery);
  const [openOnPhone, setOpenOnPhone] = useState(startsOpenOnPhone);
  const collapsed = isPhone && !openOnPhone;

  const label = (
    <>
      <span id={titleId} className={styles.title}>
        {title}
      </span>
      {badge}
      <span className={styles.count}>
        {count}
        <span className="visually-hidden"> {count === 1 ? 'item' : 'items'}</span>
      </span>
    </>
  );

  //the column is named by its title alone, the heading still reads out the badge and count
  return (
    <section
      ref={columnRef}
      className={cx(styles.column, dropState && styles[dropState])}
      aria-labelledby={titleId}
    >
      <h2 className={styles.head}>
        {isPhone ? (
          <button
            type="button"
            className={styles.toggle}
            aria-expanded={openOnPhone}
            aria-controls={bodyId}
            onClick={() => setOpenOnPhone((open) => !open)}
          >
            {label}
            <ChevronDown
              className={cx(styles.chevron, openOnPhone && styles.chevronOpen)}
              aria-hidden="true"
            />
          </button>
        ) : (
          label
        )}
      </h2>

      {/*visual only, screen readers hear the drag announcements instead*/}
      {dropState && (
        <p className={styles.dropHint} aria-hidden="true">
          {dropHints[dropState]}
        </p>
      )}

      <div id={bodyId} className={styles.body} hidden={collapsed}>
        {count === 0 ? (
          <p className={styles.empty}>{emptyText}</p>
        ) : (
          <ul className={styles.list}>{children}</ul>
        )}
      </div>
    </section>
  );
}
