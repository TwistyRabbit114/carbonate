import { useId, useState, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import { cx } from '@/lib/cx';
import { phoneQuery, useMediaQuery } from '@/lib/useMediaQuery';
import styles from './Kanban.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type KanbanColumnProps = {
  title: string;
  count: number;
  badge?: ReactNode; //e.g. the "Auto" badge on columns the server moves events into
  emptyText?: string;
  startsOpenOnPhone?: boolean;
  children: ReactNode; //the <li> cards
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
    <section className={styles.column} aria-labelledby={titleId}>
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
