import { useState } from 'react';
import { Link } from 'react-router';
import type { OrderList } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { formatDate, formatDateOnly } from '@/lib/format';
import { useOrderLists } from './api';
import { GenerateOrderListsDialog } from './GenerateOrderListsDialog';
import { generatedByMe } from './OrderListActions';
import { orderListStatusLabels } from './labels';
import styles from './StockPage.module.scss';

//the supplier lists made from the events' stock (FR-28), newest first, with where each has got
//to on its way to being placed (FR-30)
//TODO(plan): the list only says who generated it by id, and the people list needs user.manage.
//ask D to add the generator's and approver's names to OrderListDto
export function OrderListsPanel() {
  const check = usePermissions();
  const lists = useOrderLists();
  const [generating, setGenerating] = useState(false);

  return (
    <Panel
      title="Order lists"
      actions={
        check.can('order.generate') && (
          <Button variant="ghost" onClick={() => setGenerating(true)}>
            Generate order lists
          </Button>
        )
      }
    >
      {lists.isError ? (
        <ErrorState message="We couldn't load the order lists." onRetry={() => void lists.refetch()} />
      ) : lists.isPending ? (
        <p role="status">Loading the order lists…</p>
      ) : lists.data.length === 0 ? (
        <p>No order lists yet. They're made from the confirmed events' stock, a period at a time.</p>
      ) : (
        <OrderListTable lists={lists.data} />
      )}

      {generating && <GenerateOrderListsDialog onClose={() => setGenerating(false)} />}
    </Panel>
  );
}

function OrderListTable({ lists }: { lists: OrderList[] }) {
  const me = useSignedIn().user.userId;

  return (
    <Table label="Supplier lists">
      <thead>
        <tr>
          <th scope="col">Supplier</th>
          <th scope="col">Needed by</th>
          <NumberCell head>Lines</NumberCell>
          <th scope="col">Status</th>
          <th scope="col">Generated</th>
        </tr>
      </thead>
      <tbody>
        {lists.map((list) => (
          <tr key={list.orderListId}>
            <th scope="row">
              <Link to={`/stock/order-lists/${list.orderListId}`}>{list.supplierName}</Link>
              {list.warnings.length > 0 && (
                <>
                  {' '}
                  <Badge tone="warning">Check</Badge>
                </>
              )}
            </th>
            <td className={styles.nowrap}>{formatDateOnly(list.requiredByDate)}</td>
            <NumberCell>{list.lines.length}</NumberCell>
            <td>{orderListStatusLabels[list.status]}</td>
            <td>
              {formatDate(list.generatedAt)}
              <span className={styles.sub}>{generatedByMe(list, me) ? 'by you' : 'by someone else'}</span>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
