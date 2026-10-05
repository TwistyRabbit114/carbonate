import { useParams } from 'react-router';
import { SearchX } from 'lucide-react';
import { isApiError } from '@/api/problem';
import type { OrderList } from '@/api/types';
import { useSignedIn } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { BackLink } from '@/components/BackLink';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { formatDateOnly, formatDateTime, formatMoney } from '@/lib/format';
import { useOrderList } from './api';
import { generatedByMe, OrderListActions } from './OrderListActions';
import { describeWarning, formatQuantity, orderListStatusLabels } from './labels';
import styles from './StockPage.module.scss';

const back = <BackLink to="/stock">Stock & Orders</BackLink>;

//one supplier's list (FR-28) and its way through approval (FR-30)
export default function OrderListPage() {
  const { orderListId = '' } = useParams();
  const list = useOrderList(orderListId);

  if (list.isError && isApiError(list.error) && list.error.status === 404) {
    return (
      <>
        <PageHead title="Order list not found" eyebrow={back} />
        <EmptyState title="We can't find that order list" icon={SearchX}>
          <p>It may have been removed, or the link is wrong.</p>
          <LinkButton to="/stock">Back to stock and orders</LinkButton>
        </EmptyState>
      </>
    );
  }
  if (list.isError) {
    return (
      <>
        <PageHead title="Order list" eyebrow={back} />
        <ErrorState message="We couldn't load this order list." onRetry={() => void list.refetch()} />
      </>
    );
  }
  if (!list.data) return <PageSkeleton />;
  return <OrderListDetail list={list.data} />;
}

function OrderListDetail({ list }: { list: OrderList }) {
  const me = useSignedIn().user.userId;
  //the estimate is $cost: when the api leaves it off, the column isn't there at all (NFR-17)
  const showsCost = list.lines.some((line) => line.estimatedUnitCost !== undefined);

  return (
    <>
      <PageHead
        title={`${list.supplierName} order list`}
        eyebrow={back}
        actions={<OrderListActions list={list} />}
      />

      {list.warnings.length > 0 && (
        <div className={styles.alert}>
          <Alert tone="warning">
            {list.warnings.map((warning) => describeWarning(warning, '')).join(' ')}
          </Alert>
        </div>
      )}

      <Panel title="Details">
        <FieldList>
          <FieldRow label="Status">{orderListStatusLabels[list.status]}</FieldRow>
          <FieldRow label="Needed by">{formatDateOnly(list.requiredByDate)}</FieldRow>
          <FieldRow label="For events">
            {formatDateOnly(list.periodStart)} to {formatDateOnly(list.periodEnd)}
          </FieldRow>
          <FieldRow label="Generated">
            {formatDateTime(list.generatedAt)}, {generatedByMe(list, me) ? 'by you' : 'by someone else'}
          </FieldRow>
          <FieldRow label="Approved">{list.approvedAt && formatDateTime(list.approvedAt)}</FieldRow>
          <FieldRow label="Placed">{list.placedAt && formatDateTime(list.placedAt)}</FieldRow>
        </FieldList>
      </Panel>

      <Panel title="Lines">
        {list.lines.length === 0 ? (
          <p>This list has no lines.</p>
        ) : (
          <Table label="What to order">
            <thead>
              <tr>
                <th scope="col">Item</th>
                <NumberCell head>Quantity</NumberCell>
                {showsCost && <NumberCell head>Estimated unit cost</NumberCell>}
                <th scope="col">Notes</th>
              </tr>
            </thead>
            <tbody>
              {list.lines.map((line) => (
                <tr key={line.lineId}>
                  <th scope="row">{line.stockItemName}</th>
                  <NumberCell>
                    {formatQuantity(line.quantityOrdered)} {line.unit}
                  </NumberCell>
                  {showsCost && (
                    <NumberCell>
                      {line.estimatedUnitCost != null && formatMoney(line.estimatedUnitCost)}
                    </NumberCell>
                  )}
                  <td>{line.notes}</td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Panel>
    </>
  );
}
