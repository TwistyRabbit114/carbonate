import type { UserRef } from '@/api/types';
import { formatList } from '@/lib/format';
import styles from './Avatars.module.scss';

type AvatarsProps = {
  people: readonly UserRef[];
  me?: string; //their own initials read as "you"
  max?: number;
};

//"Thabo N." is TN, "Director" is D
const initialsOf = (fullName: string) =>
  fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');

//initials in circles, with the names spelt out for screen readers and on hover
export function Avatars({ people, me, max = 3 }: AvatarsProps) {
  if (people.length === 0) return <span className={styles.nobody}>Nobody assigned</span>;

  const names = people.map((person) => (person.userId === me ? 'you' : person.fullName));
  const shown = people.slice(0, max);
  const more = people.length - shown.length;

  return (
    <span className={styles.avatars} title={formatList(people.map((person) => person.fullName))}>
      <span className="visually-hidden">Assigned to {formatList(names)}</span>
      {shown.map((person) => (
        <span key={person.userId} className={styles.avatar} aria-hidden="true">
          {initialsOf(person.fullName)}
        </span>
      ))}
      {more > 0 && (
        <span className={styles.avatar} aria-hidden="true">
          +{more}
        </span>
      )}
    </span>
  );
}
