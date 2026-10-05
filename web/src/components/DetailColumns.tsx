import type { ReactNode } from 'react';
import styles from './DetailColumns.module.scss';

type DetailColumnsProps = {
  main: ReactNode;
  side: ReactNode;
};

//the prototype's detail layout: the record on the left, who and what happens next on the right.
//one column on tablets and phones, main first
export function DetailColumns({ main, side }: DetailColumnsProps) {
  return (
    <div className={styles.columns}>
      <div>{main}</div>
      <div>{side}</div>
    </div>
  );
}
