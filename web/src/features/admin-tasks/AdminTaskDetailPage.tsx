import { useState } from 'react';
import { SearchX } from 'lucide-react';
import { useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import { queryKeys } from '@/api/queryKeys';
import type { TaskCard, UserRef } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { describeRoles } from '@/auth/roles';
import { Alert } from '@/components/Alert';
import { BackLink } from '@/components/BackLink';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { DetailColumns } from '@/components/DetailColumns';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { SanitisedDescription } from '@/components/SanitisedDescription';
import { AssigneesDialog } from '@/features/boards/AssigneesDialog';
import { useCard } from '@/features/boards/cards';
import { EditCardDialog } from '@/features/boards/EditCardDialog';
import { keepFocusOnPage } from '@/lib/focus';
import { formatDate, formatList } from '@/lib/format';
import { useActiveUsers, useAdminBoard, useTaskStep } from './api';
import { ReturnTaskDialog } from './ReturnTaskDialog';
import { actionsFor, stageNames, stageOf, stageOfCard, type AdminStage } from './rules';
import styles from './AdminTaskDetailPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//one admin task: what it is, who has it, and the next step for whoever's looking (FR-19, FR-20)
export default function AdminTaskDetailPage() {
  const { cardId = '' } = useParams();
  const card = useCard(cardId);
  const stage = card.data && stageOfCard(card.data);

  if (card.isPending) return <PageSkeleton />;
  //an event board card has no admin stage, so it isn't found here either
  if ((card.isError && isApiError(card.error) && card.error.status === 404) || (card.data && !stage)) {
    return <MissingTask />;
  }
  if (card.isError) {
    return (
      <>
        <PageHead title="Admin task" eyebrow={<BackLink to="/admin-tasks">Admin Tasks</BackLink>} />
        <ErrorState message="We couldn't load this task." onRetry={() => void card.refetch()} />
      </>
    );
  }

  return <TaskDetail card={card.data} stage={stage!} />;
}

//the same answer for a task that doesn't exist and one this user can't see, as the api gives
function MissingTask() {
  return (
    <>
      <PageHead title="Task not found" eyebrow={<BackLink to="/admin-tasks">Admin Tasks</BackLink>} />
      <EmptyState title="We can't find that task" icon={SearchX}>
        <p>It may have been taken off the board, or it isn't one of yours.</p>
        <LinkButton to="/admin-tasks">Back to Admin Tasks</LinkButton>
      </EmptyState>
    </>
  );
}

//----------------------------------------------------------\\
//                              DETAIL
//----------------------------------------------------------\\

const names = (people: readonly UserRef[], me: string) =>
  formatList(people.map((person) => (person.userId === me ? 'you' : person.fullName)));

function TaskDetail({ card, stage }: { card: TaskCard; stage: AdminStage }) {
  const me = useSignedIn().user.userId;
  const check = usePermissions();
  const board = useAdminBoard();
  const step = useTaskStep();
  const [editing, setEditing] = useState(false);
  const [reassigning, setReassigning] = useState(false);
  const [returning, setReturning] = useState(false);

  const canReview = check.can('admin_task.review');
  const actions = actionsFor(card, stage, me, canReview);
  const isAssignee = card.assignees.some((person) => person.userId === me);
  const isReviewer = canReview && card.createdBy.userId === me && !isAssignee;
  const open = stage !== 'complete';
  const canEdit = open && check.can('admin_task.edit');
  const canReassign = open && check.can('admin_task.assign');

  const column = (wanted: AdminStage) =>
    board.data?.columns.find((candidate) => stageOf(candidate) === wanted);

  //the button that was pressed goes with the panel it sat in
  function take(action: 'handIn' | 'complete') {
    const to = column(action === 'handIn' ? 'review' : 'complete');
    if (!to) return;
    step.mutate({ card, action, to }, { onSuccess: keepFocusOnPage });
  }

  //TODO(plan): the prototype's hand-in panel also has a reply note and an attachment. both wait on C's
  //call about CARD_COMMENT and on card attachments (FR-22), so for now handing in is the whole step
  return (
    <>
      <PageHead
        eyebrow={
          <>
            <BackLink to="/admin-tasks">Admin Tasks</BackLink> · {stageNames[stage]}
          </>
        }
        title={card.subject}
        actions={
          (canEdit || canReassign) && (
            <>
              {canReassign && <Button onClick={() => setReassigning(true)}>Reassign</Button>}
              {canEdit && <Button onClick={() => setEditing(true)}>Edit task</Button>}
            </>
          )
        }
      />

      {card.returnedAt && card.reviewNotes && (
        <div className={styles.returned}>
          <Alert tone="warning">
            <p className={styles.returnedBy}>
              Returned by {card.returnedBy?.fullName ?? 'the manager'} on {formatDate(card.returnedAt)}
            </p>
            <p className={styles.notes}>{card.reviewNotes}</p>
          </Alert>
        </div>
      )}

      <DetailColumns
        main={
          <>
            <Panel title="Task">
              <FieldList>
                <FieldRow label="Description">
                  {card.description ? (
                    <SanitisedDescription html={card.description} />
                  ) : (
                    <span className={styles.muted}>No description</span>
                  )}
                </FieldRow>
                <FieldRow label="Priority">
                  <Priority priority={card.priority} />
                </FieldRow>
                <FieldRow label="Due">{card.dueAt ? formatDate(card.dueAt) : 'No due date'}</FieldRow>
              </FieldList>
            </Panel>

            {actions.includes('handIn') && (
              <Panel title="Hand in">
                <p className={styles.lead}>
                  When it's done, hand it in. {card.createdBy.fullName} checks it, then signs it off or sends
                  it back with notes.
                </p>
                <Button
                  variant="primary"
                  busy={step.isPending}
                  disabled={!column('review')}
                  onClick={() => take('handIn')}
                >
                  Hand in for review
                </Button>
              </Panel>
            )}
          </>
        }
        side={
          <>
            <Panel title="Assignment">
              <FieldList>
                <FieldRow label="Assigned to">{names(card.assignees, me) || 'Nobody yet'}</FieldRow>
                <FieldRow label="Assigned by">
                  {card.createdBy.userId === me ? 'You' : card.createdBy.fullName}
                </FieldRow>
                <FieldRow label="Linked event">None, a general task</FieldRow>
                {card.completedAt && stage === 'complete' && (
                  <FieldRow label="Signed off">{formatDate(card.completedAt)}</FieldRow>
                )}
              </FieldList>
            </Panel>

            {isReviewer && (
              <Panel title="Manager review">
                <ReviewStep
                  card={card}
                  stage={stage}
                  me={me}
                  busy={step.isPending}
                  canComplete={Boolean(column('complete'))}
                  onComplete={() => take('complete')}
                  onReturn={() => setReturning(true)}
                />
              </Panel>
            )}
          </>
        }
      />

      <EditCardDialog
        card={editing ? card : null}
        due="day"
        boardKey={queryKeys.adminBoard}
        onClose={() => setEditing(false)}
      />
      {reassigning && <ReassignDialog card={card} onClose={() => setReassigning(false)} />}
      <ReturnTaskDialog
        card={returning ? card : null}
        onClose={() => {
          setReturning(false);
          keepFocusOnPage();
        }}
      />
    </>
  );
}

//----------------------------------------------------------\\
//                              PIECES
//----------------------------------------------------------\\

function Priority({ priority }: { priority: TaskCard['priority'] }) {
  if (priority === 'High') return <Badge tone="warning">High</Badge>;
  if (priority === 'Critical') return <Badge tone="danger">Critical</Badge>;
  return <>{priority}</>;
}

type ReviewStepProps = {
  card: TaskCard;
  stage: AdminStage;
  me: string;
  busy: boolean;
  canComplete: boolean;
  onComplete: () => void;
  onReturn: () => void;
};

//what the manager who handed the task out can do at each stage. only they sign it off (FR-20)
function ReviewStep({ card, stage, me, busy, canComplete, onComplete, onReturn }: ReviewStepProps) {
  const assignees = names(card.assignees, me);

  if (stage === 'assigned') return <p className={styles.lead}>Waiting for {assignees} to hand it in.</p>;
  if (stage === 'complete') {
    return (
      <p className={styles.lead}>
        You signed this off{card.completedAt && ` on ${formatDate(card.completedAt)}`}.
      </p>
    );
  }

  return (
    <>
      <p className={styles.lead}>
        {assignees} handed this in. Sign it off, or send it back with notes on what still needs doing.
      </p>
      <div className={styles.buttons}>
        <Button onClick={onReturn} disabled={busy}>
          Return with notes…
        </Button>
        <Button variant="primary" busy={busy} disabled={!canComplete} onClick={onComplete}>
          Mark complete
        </Button>
      </div>
    </>
  );
}

//the same people the assign dialog offers: only the director and ops reassign, and both can list users
function ReassignDialog({ card, onClose }: { card: TaskCard; onClose: () => void }) {
  const people = useActiveUsers();

  return (
    <AssigneesDialog
      card={card}
      people={people.data?.map((person) => ({
        userId: person.userId,
        fullName: person.fullName,
        detail: describeRoles(person.roles),
      }))}
      failed={people.isError}
      required
      hint="Whoever you pick can hand it in. You can't sign off a task you're on yourself."
      boardKey={queryKeys.adminBoard}
      onClose={onClose}
    />
  );
}
