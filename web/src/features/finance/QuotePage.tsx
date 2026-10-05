import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { SearchX } from 'lucide-react';
import { isApiError } from '@/api/problem';
import type { Quote } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { Badge } from '@/components/Badge';
import { BackLink } from '@/components/BackLink';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { useToast } from '@/components/toast/ToastContext';
import { useEvent } from '@/features/events/api';
import { isConflict, problemMessage } from '@/lib/apiErrors';
import { cx } from '@/lib/cx';
import { formatDateOnly, formatDateTime, formatMoney } from '@/lib/format';
import { currentQuote, useEventQuotes, useQuote, useQuoteStep, type QuoteStep } from './api';
import { categoryLabels, outsideBand, percent, quoteStatusLabels } from './labels';
import { QuoteLinesEditor } from './QuoteLinesEditor';
import styles from './QuotePage.module.scss';

const back = <BackLink to="/finance">Quotes & Invoices</BackLink>;

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//one costing: its lines, what it comes to, and the steps from draft to the client's yes (FR-11,
//FR-14). every figure is one the api sent, a role without the tier gets no row for it (NFR-17)
export default function QuotePage() {
  const { quoteId = '' } = useParams();
  const quote = useQuote(quoteId);

  if (quote.isError && isApiError(quote.error) && quote.error.status === 404) {
    return (
      <>
        <PageHead title="Costing not found" eyebrow={back} />
        <EmptyState title="We can't find that costing" icon={SearchX}>
          <p>It may have been removed, or it isn't shared with you.</p>
          <LinkButton to="/finance">Back to quotes and invoices</LinkButton>
        </EmptyState>
      </>
    );
  }
  if (quote.isError) {
    return (
      <>
        <PageHead title="Costing" eyebrow={back} />
        <ErrorState message="We couldn't load this costing." onRetry={() => void quote.refetch()} />
      </>
    );
  }
  if (!quote.data) return <PageSkeleton />;
  return <QuoteDetail key={quote.data.rowVersion} quote={quote.data} onReload={() => void quote.refetch()} />;
}

//----------------------------------------------------------\\
//                              DETAIL
//----------------------------------------------------------\\

const stepDone: Record<QuoteStep, string> = {
  submit: 'is with the Director for approval',
  approve: 'is approved',
  issue: 'is marked as sent to the client',
  accept: 'is accepted',
};

function QuoteDetail({ quote, onReload }: { quote: Quote; onReload: () => void }) {
  const check = usePermissions();
  const event = useEvent(quote.eventId);
  const versions = useEventQuotes(quote.eventId, quote.status === 'Superseded');
  const step = useQuoteStep();
  const navigate = useNavigate();
  const toast = useToast();
  const [editing, setEditing] = useState(false);

  const eventName = event.data?.name ?? 'Event';
  const canEdit = check.can('quote.edit');
  const editable =
    quote.status === 'Draft' || quote.status === 'PendingApproval' || quote.status === 'Approved';
  //only a costing that hasn't gone out yet can still be waiting for the director
  const waitingForDirector =
    (quote.status === 'Draft' || quote.status === 'PendingApproval') &&
    quote.requiresApproval &&
    !quote.approvedByUserId;
  const latest = currentQuote(versions.data);

  async function take(next: QuoteStep) {
    try {
      const saved = await step.mutateAsync({ quote, step: next });
      //submitting their own costing approves it for the director (FR-14)
      toast.success(
        `Version ${saved.version} ${next === 'submit' && saved.status === 'Approved' ? stepDone.approve : stepDone[next]}.`,
      );
    } catch (error) {
      if (isConflict(error)) {
        toast.error('Someone else changed this costing. It has been reloaded, have another look.');
        onReload();
      } else {
        toast.error(problemMessage(error, 'Nothing changed.'));
      }
    }
  }

  const stepButton = (next: QuoteStep, label: string, primary = false) => (
    <Button
      variant={primary ? 'primary' : 'default'}
      busy={step.isPending && step.variables?.step === next}
      onClick={() => void take(next)}
    >
      {label}
    </Button>
  );

  //the next step for this status, for whoever may take it
  const actions = editing ? null : (
    <>
      {quote.status === 'Draft' &&
        canEdit &&
        stepButton(
          'submit',
          check.can('quote.approve') ? 'Approve' : 'Submit for approval',
          waitingForDirector,
        )}
      {quote.status === 'PendingApproval' &&
        check.can('quote.approve') &&
        stepButton('approve', 'Approve', true)}
      {(quote.status === 'Approved' || (quote.status === 'Draft' && !waitingForDirector)) &&
        canEdit &&
        stepButton('issue', 'Mark as sent to client', true)}
      {quote.status === 'Issued' && canEdit && stepButton('accept', 'Client accepted', true)}
    </>
  );

  return (
    <>
      <PageHead
        title={`${eventName} costing`}
        eyebrow={
          <>
            {back} · Version {quote.version}
          </>
        }
        actions={actions}
      />

      {quote.status === 'Superseded' && (
        <div className={styles.alert}>
          <Alert>
            <p className={styles.alertText}>A newer version replaced this one. It's kept as it was sent.</p>
            {latest && latest.quoteId !== quote.quoteId && (
              <LinkButton to={`/quotes/${latest.quoteId}`}>Open version {latest.version}</LinkButton>
            )}
          </Alert>
        </div>
      )}
      {waitingForDirector && (
        <div className={styles.alert}>
          <Alert tone="warning">
            {quote.status === 'PendingApproval'
              ? "Waiting for the Director. It's above the approval limit, so it can't be sent until it's approved."
              : "This is above the approval limit, so the Director has to approve it before it's sent."}
          </Alert>
        </div>
      )}

      <div className={cx(styles.columns, editing && styles.stacked)}>
        <Panel
          title="Lines"
          actions={
            !editing &&
            canEdit &&
            (editable || quote.status === 'Issued') && (
              <Button variant="ghost" onClick={() => setEditing(true)}>
                {quote.status === 'Issued' ? 'Make a new version' : 'Change lines'}
              </Button>
            )
          }
        >
          {editing ? (
            <QuoteLinesEditor
              quote={quote}
              onCancel={() => setEditing(false)}
              onReload={() => {
                setEditing(false);
                onReload();
              }}
              onSaved={(saved) => {
                setEditing(false);
                if (saved.quoteId !== quote.quoteId)
                  void navigate(`/quotes/${saved.quoteId}`, { replace: true });
              }}
            />
          ) : (
            <QuoteLines quote={quote} />
          )}
        </Panel>

        <QuoteTotals quote={quote} />
      </div>
    </>
  );
}

//----------------------------------------------------------\\
//                              READING
//----------------------------------------------------------\\

function QuoteLines({ quote }: { quote: Quote }) {
  if (quote.lines.length === 0) return <p>No lines yet. Change lines to add what the event needs.</p>;
  //each column only when the api sent its figures (NFR-17)
  const showsCost = quote.lines.some((line) => line.unitCostToUs != null);
  const showsPrice = quote.lines.some((line) => line.unitPriceToClient != null);
  const showsTotal = quote.lines.some((line) => line.lineTotal != null);

  return (
    <Table label="Costing lines">
      <thead>
        <tr>
          <th scope="col">Line</th>
          <NumberCell head>Quantity</NumberCell>
          {showsCost && <NumberCell head>Unit cost to us</NumberCell>}
          {showsPrice && <NumberCell head>Unit price to client</NumberCell>}
          {showsTotal && <NumberCell head>Line total</NumberCell>}
        </tr>
      </thead>
      <tbody>
        {quote.lines.map((line) => (
          <tr key={line.quoteLineId}>
            <th scope="row">
              {line.description}
              <span className={styles.sub}>{categoryLabels[line.category]}</span>
            </th>
            <NumberCell>{new Intl.NumberFormat('en-ZA').format(line.quantity)}</NumberCell>
            {showsCost && (
              <NumberCell>{line.unitCostToUs != null && formatMoney(line.unitCostToUs)}</NumberCell>
            )}
            {showsPrice && (
              <NumberCell>{line.unitPriceToClient != null && formatMoney(line.unitPriceToClient)}</NumberCell>
            )}
            {showsTotal && <NumberCell>{line.lineTotal != null && formatMoney(line.lineTotal)}</NumberCell>}
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function QuoteTotals({ quote }: { quote: Quote }) {
  const band = quote.targetMarginBand;
  const showsCost = quote.internalCostTotal != null || quote.marginPercent != null;

  return (
    <Panel title="Totals">
      <FieldList>
        <FieldRow label="Status">{quoteStatusLabels[quote.status]}</FieldRow>
        <FieldRow label="Excl. VAT">
          {quote.subtotalExVat != null && formatMoney(quote.subtotalExVat)}
        </FieldRow>
        <FieldRow label="VAT">{quote.vatAmount != null && formatMoney(quote.vatAmount)}</FieldRow>
        <FieldRow label="Price to client">
          {quote.totalIncVat != null && <strong>{formatMoney(quote.totalIncVat)}</strong>}
        </FieldRow>
        <FieldRow label="Valid until">{quote.validUntil && formatDateOnly(quote.validUntil)}</FieldRow>
        <FieldRow label="Approved">{quote.approvedAt && formatDateTime(quote.approvedAt)}</FieldRow>
        <FieldRow label="Sent">{quote.issuedAt && formatDateTime(quote.issuedAt)}</FieldRow>
        <FieldRow label="Accepted">{quote.acceptedAt && formatDateTime(quote.acceptedAt)}</FieldRow>
        <FieldRow label="Started as">
          {quote.copiedFromQuoteId && 'A copy of an earlier costing for this client'}
        </FieldRow>
      </FieldList>

      {showsCost && (
        <div className={styles.cost}>
          <FieldList>
            <FieldRow label="Internal cost">
              {quote.internalCostTotal != null && formatMoney(quote.internalCostTotal)}
            </FieldRow>
            <FieldRow label="Margin">
              {quote.marginPercent != null && (
                <>
                  {percent(quote.marginPercent)}
                  {band?.targetMinPct != null && band.targetMaxPct != null && (
                    <span className={styles.muted}>
                      {' '}
                      · target {percent(band.targetMinPct)} to {percent(band.targetMaxPct)}
                    </span>
                  )}{' '}
                  {outsideBand(quote) && <Badge tone="warning">Outside target</Badge>}
                </>
              )}
            </FieldRow>
          </FieldList>
          <p className={styles.note}>
            Internal cost and margin are visible to Director, Event Manager and Accounts only.
          </p>
        </div>
      )}
    </Panel>
  );
}
