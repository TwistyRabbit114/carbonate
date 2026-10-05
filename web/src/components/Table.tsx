import { useEffect, useRef, useState, type ReactNode } from 'react';
import styles from './Table.module.scss';

type TableProps = {
  label: string; //the caption, read out by screen readers
  children: ReactNode; //thead and tbody
};

//the prototype's table. on a phone it scrolls sideways inside its box rather than pushing the
//page wider, and while it does, the box takes focus so the keyboard can scroll it too
export function Table({ label, children }: TableProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [scrolls, setScrolls] = useState(false);

  useEffect(() => {
    const box = ref.current;
    if (!box) return;
    const check = () => setScrolls(box.scrollWidth > box.clientWidth);
    check();
    if (typeof ResizeObserver === 'undefined') return;
    //the box can narrow, or the table inside it can grow as rows arrive
    const observer = new ResizeObserver(check);
    observer.observe(box);
    if (box.firstElementChild) observer.observe(box.firstElementChild);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      className={styles.scroll}
      role="region"
      aria-label={label}
      //eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- a box that scrolls has to take focus, or the keyboard can't reach what's off to the side
      tabIndex={scrolls ? 0 : undefined}
    >
      <table className={styles.table}>
        <caption className={styles.caption}>{label}</caption>
        {children}
      </table>
    </div>
  );
}

//numbers and money sit right, so the digits line up down a column
export function NumberCell({ children, head = false }: { children?: ReactNode; head?: boolean }) {
  const Cell = head ? 'th' : 'td';
  return (
    <Cell className={styles.number} scope={head ? 'col' : undefined}>
      {children}
    </Cell>
  );
}
