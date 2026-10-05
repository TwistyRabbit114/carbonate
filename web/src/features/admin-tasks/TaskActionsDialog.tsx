import type { TaskCard } from '@/api/types';
import { Button } from '@/components/Button';
import { ChoiceList, type Choice } from '@/components/ChoiceList';
import { Dialog, DialogFooter } from '@/components/Dialog';
import type { PlacedTask, TaskAction } from './rules';
import styles from './TaskDialogs.module.scss';

type TaskActionsDialogProps = {
  task: PlacedTask | null; //open while a task is set
  onClose: () => void;
  onAct: (task: PlacedTask, action: TaskAction) => void;
};

//each step with what it does, in the words the assignee and the manager would use
function choiceFor(card: TaskCard, action: TaskAction): Omit<Choice, 'onSelect'> {
  switch (action) {
    case 'handIn':
      return {
        key: action,
        title: 'Hand in for review',
        hint: `${card.createdBy.fullName} checks it, then signs it off or sends it back.`,
      };
    case 'complete':
      return { key: action, title: 'Mark complete', hint: 'Signs it off and moves it to Complete.' };
    case 'return':
      return { key: action, title: 'Return with notes…', hint: 'Sends it back to Assigned with what still needs doing.' };
  }
}

//the "Move…" alternative to dragging, for the keyboard, a screen reader, or a phone
export function TaskActionsDialog({ task, onClose, onAct }: TaskActionsDialogProps) {
  if (!task) return null;

  return (
    <Dialog open title={`Move ${task.card.subject}`} onClose={onClose}>
      <p className={styles.lead}>
        It's in <strong>{task.columnName}</strong> now.
      </p>
      <ChoiceList
        choices={task.actions.map((action) => ({
          ...choiceFor(task.card, action),
          onSelect: () => onAct(task, action),
        }))}
      />
      <DialogFooter>
        <Button onClick={onClose}>Close</Button>
      </DialogFooter>
    </Dialog>
  );
}
