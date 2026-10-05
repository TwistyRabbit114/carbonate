import { Link } from 'react-router';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { useOrderLists } from '@/features/stock/api';
import { OrderListActions } from '@/features/stock/OrderListActions';
import { formatDate, formatDateOnly } from '@/lib/format';
import styles from './FinancePage.module.scss';

//order lists waiting for someone with order.approve, who isn't whoever generated them (FR-30)
//TODO(plan): the prototype shows each list's value, but OrderListDto has no total and the spa
//doesn't work money out itself. ask D for an estimated total ($cost) on the list
export function ApprovalsPanel() {
  const lists = useOrderLists('PendingApproval');

  return (
    <Panel title="Order list approvals">
      {lists.isError ? (
        <ErrorState message="We couldn't load the order lists." onRetry={() => void lists.refetch()} />
      ) : lists.isPending ? (
        <p role="status">Loading the order lists…</p>
      ) : lists.data.length === 0 ? (
        <p>Nothing waiting for approval.</p>
      ) : (
        <Table label="Lists waiting for approval">
          <thead>
            <tr>
              <th scope="col">Supplier</th>
              <th scope="col">Needed by</th>
              <NumberCell head>Lines</NumberCell>
              <th scope="col">Generated</th>
              <th scope="col">
                <span className={styles.hidden}>Approve</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {lists.data.map((list) => (
              <tr key={list.orderListId}>
                <th scope="row">
                  <Link to={`/stock/order-lists/${list.orderListId}`}>{list.supplierName}</Link>
                </th>
                <td className={styles.nowrap}>{formatDateOnly(list.requiredByDate)}</td>
                <NumberCell>{list.lines.length}</NumberCell>
                <td>{formatDate(list.generatedAt)}</td>
                <td>
                  <OrderListActions list={list} compact />
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Panel>
  );
}
