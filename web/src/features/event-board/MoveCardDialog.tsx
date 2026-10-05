import type { BoardColumn, TaskCard } from '@/api/types';
import { Button } from '@/components/Button';
import { ChoiceList } from '@/components/ChoiceList';
import { Dialog, DialogFooter } from '@/components/Dialog';
import styles from '@/features/boards/CardDialogs.module.scss';

type MoveCardDialogProps = {
  card: TaskCard | null; //open while a card is set
  columns: readonly BoardColumn[];
  onClose: () => void;
  onMove: (card: TaskCard, to: BoardColumn) => void;
};

//what moving there means, when it means more than changing column. the limit is display only (FR-23)
function hintFor(card: TaskCard, from: BoardColumn | undefined, to: BoardColumn) {
  const others = to.cards.filter((other) => other.cardId !== card.cardId).length;
  const full = to.wipLimit && others >= to.wipLimit ? `It's at its limit of ${to.wipLimit} already. ` : '';
  if (to.isDoneColumn) return `${full}Marks it done.`;
  if (from?.isDoneColumn) return `${full}Opens it again.`;
  return full.trim() || undefined;
}

//the "Move to…" alternative to dragging, for the keyboard, a screen reader, or a phone
export function MoveCardDialog({ card, columns, onClose, onMove }: MoveCardDialogProps) {
  if (!card) return null;
  const from = columns.find((column) => column.columnId === card.columnId);

  return (
    <Dialog open title={`Move ${card.subject}`} onClose={onClose}>
      <p className={styles.lead}>
        It's in <strong>{from?.name ?? 'a column that has gone'}</strong> now.
      </p>
      <ChoiceList
        choices={columns
          .filter((column) => column.columnId !== card.columnId)
          .map((column) => ({
            key: column.columnId,
            title: `Move to ${column.name}`,
            hint: hintFor(card, from, column),
            onSelect: () => onMove(card, column),
          }))}
      />
      <DialogFooter>
        <Button onClick={onClose}>Close</Button>
      </DialogFooter>
    </Dialog>
  );
}
