import { useId } from 'react';
import { Alert } from '@/components/Alert';
import styles from './CardDialogs.module.scss';

export type PersonOption = {
  userId: string;
  fullName: string;
  detail?: string; //their role or crew role, to tell two people apart
};

type PeoplePickerProps = {
  legend: string;
  people: readonly PersonOption[] | undefined; //undefined while loading
  failed?: boolean;
  selected: readonly string[];
  onChange: (userIds: string[]) => void;
  me: string;
  hint?: string;
  error?: string;
};

//a tick per person rather than a multi-select box, which is awkward on a phone and with a screen reader
export function PeoplePicker({
  legend,
  people,
  failed = false,
  selected,
  onChange,
  me,
  hint,
  error,
}: PeoplePickerProps) {
  const id = useId();
  const describedBy = [hint && `${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ') || undefined;

  function toggle(userId: string, on: boolean) {
    onChange(on ? [...selected, userId] : selected.filter((other) => other !== userId));
  }

  return (
    <fieldset className={styles.people} aria-describedby={describedBy}>
      <legend className={styles.legend}>{legend}</legend>

      {failed ? (
        <Alert tone="danger">Couldn't load the list of people. Close this and try again.</Alert>
      ) : !people ? (
        <p className={styles.hint}>Loading people…</p>
      ) : (
        <ul className={styles.list}>
          {people.map((person) => (
            <li key={person.userId}>
              <label className={styles.person}>
                <input
                  type="checkbox"
                  checked={selected.includes(person.userId)}
                  onChange={(event) => toggle(person.userId, event.target.checked)}
                />
                <span>
                  <span className={styles.name}>
                    {person.fullName}
                    {person.userId === me && ' (you)'}
                  </span>
                  {person.detail && <span className={styles.detail}> · {person.detail}</span>}
                </span>
              </label>
            </li>
          ))}
        </ul>
      )}

      {hint && (
        <p id={`${id}-hint`} className={styles.hint}>
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className={styles.error}>
          {error}
        </p>
      )}
    </fieldset>
  );
}
