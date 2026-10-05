import styles from './ChoiceList.module.scss';

export type Choice = {
  key: string;
  title: string;
  hint?: string; //what happens if you pick it, in a line underneath
  onSelect: () => void;
};

//a big button per option with the reason under it, so the choice is obvious. used by the
//"Move to…" dialogs, the keyboard and phone alternative to dragging a card
export function ChoiceList({ choices }: { choices: readonly Choice[] }) {
  return (
    <ul className={styles.list}>
      {choices.map((choice) => (
        <li key={choice.key}>
          <button type="button" className={styles.choice} onClick={choice.onSelect}>
            <span className={styles.title}>{choice.title}</span>
            {choice.hint && <span className={styles.hint}>{choice.hint}</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}
