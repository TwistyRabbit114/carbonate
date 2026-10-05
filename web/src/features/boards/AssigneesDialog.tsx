import { useState, type FormEvent } from 'react';
import type { QueryKey } from '@tanstack/react-query';
import { isApiError } from '@/api/problem';
import type { TaskCard } from '@/api/types';
import { useSignedIn } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { useToast } from '@/components/toast/ToastContext';
import { formatList, sentence } from '@/lib/format';
import { currentCardIn, describeCardError, useSetAssignees } from './cards';
import { PeoplePicker, type PersonOption } from './PeoplePicker';
import styles from './CardDialogs.module.scss';

type AssigneesDialogProps = {
  card: TaskCard | null; //open while a card is set
  people: readonly PersonOption[] | undefined;
  failed: boolean;
  required: boolean; //an admin task always has someone on it
  hint?: string;
  boardKey: QueryKey;
  onClose: () => void;
};

export function AssigneesDialog({ card, ...rest }: AssigneesDialogProps) {
  if (!card) return null;
  return <AssigneesForm key={card.cardId} card={card} {...rest} />;
}

//people already on the card stay in the list even if they've since left the crew or the list
//of active users, so they can still be taken off
function withCurrent(people: readonly PersonOption[] | undefined, card: TaskCard) {
  if (!people) return undefined;
  const missing = card.assignees.filter(
    (person) => !people.some((option) => option.userId === person.userId),
  );
  return [...people, ...missing.map((person) => ({ ...person, detail: 'On the card now' }))];
}

function AssigneesForm({
  card,
  people,
  failed,
  required,
  hint,
  boardKey,
  onClose,
}: AssigneesDialogProps & { card: TaskCard }) {
  const me = useSignedIn().user.userId;
  const setAssignees = useSetAssignees(boardKey);
  const toast = useToast();
  const [selected, setSelected] = useState(() => card.assignees.map((person) => person.userId));
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [notice, setNotice] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const options = withCurrent(people, card);

  async function save(event: FormEvent) {
    event.preventDefault();
    setFieldError(undefined);
    setNotice(null);
    setServerError(null);
    if (required && selected.length === 0) {
      setFieldError('Choose who should do this task.');
      return;
    }

    try {
      //the card from the page, so its row version is whatever the page last heard
      const saved = await setAssignees.mutateAsync({ card, userIds: selected });
      const names = formatList(saved.assignees.map((person) => person.fullName));
      toast.success(
        sentence(names ? `${saved.subject} is with ${names} now` : `Nobody is on ${saved.subject} now`),
      );
      onClose();
    } catch (error) {
      const current = currentCardIn(error);
      if (current) {
        setSelected(current.assignees.map((person) => person.userId));
        setNotice(
          "Someone else changed this card just now. The ticks show who's on it now, so check them and save again if you still want your change.",
        );
        return;
      }
      const errors = isApiError(error) && error.status === 400 ? error.problem.errors : undefined;
      const message = errors?.userIds?.[0] ?? errors?.assigneeIds?.[0];
      if (message) setFieldError(message);
      else setServerError(describeCardError(error, card.subject));
    }
  }

  return (
    <Dialog open title={`People on ${card.subject}`} onClose={onClose}>
      {notice && (
        <div className={styles.alert}>
          <Alert tone="warning">{notice}</Alert>
        </div>
      )}
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      <form onSubmit={save} noValidate>
        <PeoplePicker
          legend="Assigned to"
          people={options}
          failed={failed}
          selected={selected}
          onChange={setSelected}
          me={me}
          hint={hint}
          error={fieldError}
        />
        <DialogFooter>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="primary" busy={setAssignees.isPending} disabled={!options}>
            Save
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
