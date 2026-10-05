import { useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import type { OrderList } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { useToast } from '@/components/toast/ToastContext';
import { isConflict, problemMessage } from '@/lib/apiErrors';
import { useOrderListStep, type OrderListStep } from './api';
import styles from './StockPage.module.scss';

const done: Record<OrderListStep, string> = {
  submit: 'is waiting for approval',
  approve: 'is approved',
  'mark-placed': 'is marked as placed',
};

//whoever made a list can't sign it off (FR-30), so they're told why instead of getting a button
export function generatedByMe(list: OrderList, myUserId: string) {
  return list.generatedByUserId === myUserId;
}

type OrderListActionsProps = {
  list: OrderList;
  compact?: boolean; //in a table row, where the explanation is shorter
};

//the next step for the list, if this user can take it: submit a draft (order.generate), approve
//one that's waiting (order.approve), mark an approved one placed (order.place)
export function OrderListActions({ list, compact = false }: OrderListActionsProps) {
  const check = usePermissions();
  const me = useSignedIn().user.userId;
  const step = useOrderListStep();
  const queryClient = useQueryClient();
  const toast = useToast();

  async function take(next: OrderListStep) {
    try {
      await step.mutateAsync({ list, step: next });
      toast.success(`The ${list.supplierName} order list ${done[next]}.`);
    } catch (error) {
      if (isConflict(error)) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.orderLists });
        toast.error(
          'Someone changed this order list in the meantime. It has been reloaded, have another look.',
        );
      } else {
        toast.error(problemMessage(error, 'Nothing changed.'));
      }
    }
  }

  const label = list.supplierName;

  if (list.status === 'Draft' && check.can('order.generate')) {
    return (
      <Button
        busy={step.isPending}
        onClick={() => void take('submit')}
        aria-label={compact ? `Submit the ${label} list for approval` : undefined}
      >
        Submit for approval
      </Button>
    );
  }
  if (list.status === 'PendingApproval' && check.can('order.approve')) {
    if (generatedByMe(list, me)) {
      return (
        <p className={styles.note}>
          {compact ? 'You generated it' : 'You generated this list, so someone else needs to approve it.'}
        </p>
      );
    }
    return (
      <Button
        variant="primary"
        busy={step.isPending}
        onClick={() => void take('approve')}
        aria-label={compact ? `Approve the ${label} list` : undefined}
      >
        Approve
      </Button>
    );
  }
  if (list.status === 'Approved' && check.can('order.place')) {
    return (
      <Button
        busy={step.isPending}
        onClick={() => void take('mark-placed')}
        aria-label={compact ? `Mark the ${label} list placed` : undefined}
      >
        Mark placed
      </Button>
    );
  }
  return null;
}
