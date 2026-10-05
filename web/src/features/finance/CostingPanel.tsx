import type { EventDetail, Quote } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { FieldList, FieldRow } from '@/components/FieldList';
import { Panel } from '@/components/Panel';
import { formatMoney } from '@/lib/format';
import { currentQuote, useEventQuotes } from './api';
import styles from './CostingPanel.module.scss';

//----------------------------------------------------------\
//                              LABELS
//----------------------------------------------------------\

const quoteStatusLabels: Record<Quote['status'], string> = {
  Draft: 'Draft',
  PendingApproval: 'Waiting for the Director',
  Approved: 'Approved',
  Issued: 'Sent to the client',
  Accepted: 'Accepted by the client',
  Superseded: 'Replaced by a newer version',
};

const percent = (value: number) => `${Math.round(value * 10) / 10}%`;

//----------------------------------------------------------\
//                              PANEL
//----------------------------------------------------------\

//money for the event (FR-11, FR-14). every figure here is a field the api leaves out for roles
//without it, so a role that can't see prices gets no panel at all, not an empty one (NFR-17)
//TODO(plan): link to the costing once the quote screen is built
export function CostingPanel({ event }: { event: EventDetail }) {
  //quote.view decides whether to ask at all, the api decides what comes back
  const quotes = useEventQuotes(event.eventId, usePermissions().can('quote.view'));
  const quote = currentQuote(quotes.data);

  const hasBudget = event.budgetAmount !== undefined;
  const hasPrice = quote?.subtotalExVat != null;
  if (!hasBudget && !hasPrice) return null;

  const band = quote?.targetMarginBand;
  const margin = quote?.marginPercent;
  const outsideBand =
    margin != null &&
    ((band?.targetMinPct != null && margin < band.targetMinPct) ||
      (band?.targetMaxPct != null && margin > band.targetMaxPct));
  const showsCost = quote?.internalCostTotal != null || margin != null;

  return (
    <Panel title="Costing">
      <FieldList>
        {hasBudget && (
          <FieldRow label="Budget">
            {event.budgetAmount == null ? 'Not set' : formatMoney(event.budgetAmount)}
          </FieldRow>
        )}
        {quote && (
          <FieldRow label="Costing">
            Version {quote.version} · {quoteStatusLabels[quote.status]}
          </FieldRow>
        )}
        {quote?.subtotalExVat != null && (
          <FieldRow label="Price to client">
            <strong>{formatMoney(quote.subtotalExVat)}</strong>{' '}
            <span className={styles.muted}>excl. VAT</span>
          </FieldRow>
        )}
        {quote?.totalIncVat != null && (
          <FieldRow label="Incl. VAT">{formatMoney(quote.totalIncVat)}</FieldRow>
        )}
      </FieldList>

      {showsCost && (
        <div className={styles.cost}>
          <FieldList>
            {quote?.internalCostTotal != null && (
              <FieldRow label="Internal cost">{formatMoney(quote.internalCostTotal)}</FieldRow>
            )}
            {margin != null && (
              <FieldRow label="Margin">
                {percent(margin)}
                {band?.targetMinPct != null && band.targetMaxPct != null && (
                  <span className={styles.muted}>
                    {' '}
                    · target {percent(band.targetMinPct)} to {percent(band.targetMaxPct)}
                  </span>
                )}{' '}
                {outsideBand && <Badge tone="warning">Outside target</Badge>}
              </FieldRow>
            )}
          </FieldList>
        </div>
      )}

      {quotes.data && !quote && <p className={styles.note}>No costing yet.</p>}
      {showsCost && (
        <p className={styles.note}>
          Internal cost and margin are visible to Director, Event Manager and Accounts only.
        </p>
      )}
    </Panel>
  );
}
