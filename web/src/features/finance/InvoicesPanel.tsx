import { useState } from 'react';
import type { Invoice, InvoiceStatus } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Field } from '@/components/Field';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { formatDateOnly, formatMoney } from '@/lib/format';
import { useInvoices } from './api';
import { invoiceStatusLabels } from './labels';
import { MarkPaidDialog } from './MarkPaidDialog';
import styles from './FinancePage.module.scss';

const statuses: InvoiceStatus[] = ['Issued', 'Paid', 'Draft', 'Void'];

//every invoice with the PO or deposit it was raised against (FR-13). invoice.manage marks them paid
//TODO(plan): raising an invoice needs the event's confirmation id, and nothing the spa can read
//gives it. ask C to put the confirmation on the event detail, or to finish uninvoiced-events (FR-16)
export function InvoicesPanel() {
  const [status, setStatus] = useState<InvoiceStatus | ''>('');
  const invoices = useInvoices(status);
  const canManage = usePermissions().can('invoice.manage');
  const [paying, setPaying] = useState<Invoice | null>(null);
  //$price: no amount from the api, no amount column (NFR-17)
  const showsAmount = invoices.data?.some((invoice) => invoice.amountIncVat !== undefined) ?? false;

  return (
    <Panel title="Invoices">
      <div className={styles.filters}>
        <Field label="Show">
          {(field) => (
            <select
              {...field}
              value={status}
              onChange={(change) => setStatus(change.target.value as InvoiceStatus | '')}
            >
              <option value="">Every invoice</option>
              {statuses.map((value) => (
                <option key={value} value={value}>
                  {invoiceStatusLabels[value]}
                </option>
              ))}
            </select>
          )}
        </Field>
      </div>

      {invoices.isError ? (
        <ErrorState message="We couldn't load the invoices." onRetry={() => void invoices.refetch()} />
      ) : invoices.isPending ? (
        <p role="status">Loading the invoices…</p>
      ) : invoices.data.length === 0 ? (
        <p>{status ? 'No invoices with that status.' : 'No invoices yet.'}</p>
      ) : (
        <Table label="Invoice list">
          <thead>
            <tr>
              <th scope="col">Invoice</th>
              <th scope="col">Against</th>
              <th scope="col">Sent</th>
              <th scope="col">Due</th>
              {showsAmount && <NumberCell head>Amount incl. VAT</NumberCell>}
              <th scope="col">Status</th>
              {canManage && (
                <th scope="col">
                  <span className={styles.hidden}>Change</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {invoices.data.map((invoice) => (
              <tr key={invoice.invoiceId}>
                <th scope="row">
                  {invoice.invoiceNumber}
                  <span className={styles.sub}>{invoice.eventCode}</span>
                </th>
                <td>{invoice.confirmationReference}</td>
                <td className={styles.nowrap}>{formatDateOnly(invoice.issuedDate)}</td>
                <td className={styles.nowrap}>{formatDateOnly(invoice.dueDate)}</td>
                {showsAmount && (
                  <NumberCell>{invoice.amountIncVat != null && formatMoney(invoice.amountIncVat)}</NumberCell>
                )}
                <td>
                  {invoiceStatusLabels[invoice.status]}
                  {invoice.paidDate && (
                    <span className={styles.sub}>on {formatDateOnly(invoice.paidDate)}</span>
                  )}
                </td>
                {canManage && (
                  <td>
                    {invoice.status === 'Issued' && (
                      <Button
                        variant="ghost"
                        onClick={() => setPaying(invoice)}
                        aria-label={`Mark ${invoice.invoiceNumber} paid`}
                      >
                        Mark paid
                      </Button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {paying && <MarkPaidDialog invoice={paying} onClose={() => setPaying(null)} />}
    </Panel>
  );
}
