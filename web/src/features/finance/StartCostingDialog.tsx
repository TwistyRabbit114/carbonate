import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { EventListItem } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { problemMessage } from '@/lib/apiErrors';
import { formatDateOnly, formatMoney } from '@/lib/format';
import { currentQuote, useCostHistory, useEventQuotes, useStartCosting } from './api';
import form from '@/components/FormLayout.module.scss';

type StartCostingDialogProps = {
  event: Pick<EventListItem, 'eventId' | 'name'>;
  onClose: () => void;
};

//a new costing starts blank, or as a copy of one from an earlier event for the same client, with
//its lines, quantities and prices (FR-15). the copy gets today's vat, worked out by the api
export function StartCostingDialog({ event, onClose }: StartCostingDialogProps) {
  const navigate = useNavigate();
  const start = useStartCosting(event.eventId);
  const [copy, setCopy] = useState(false);
  const [sourceEventId, setSourceEventId] = useState('');
  const [serverError, setServerError] = useState<string | null>(null);
  const history = useCostHistory(event.eventId, copy);
  const sourceQuotes = useEventQuotes(sourceEventId, copy && Boolean(sourceEventId));
  const source = currentQuote(sourceQuotes.data);
  const earlier = (history.data ?? []).filter((item) => item.totalIncVat != null);

  async function create() {
    setServerError(null);
    try {
      const quote = await start.mutateAsync({ sourceQuoteId: copy ? (source?.quoteId ?? null) : null });
      onClose();
      void navigate(`/quotes/${quote.quoteId}`);
    } catch (error) {
      setServerError(problemMessage(error, 'No costing was started.', "Your role can't make costings."));
    }
  }

  const waitingForSource = copy && (!sourceEventId || !source);

  return (
    <Dialog open title={`Start a costing for ${event.name}`} onClose={onClose}>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <fieldset className={form.checks}>
        <legend className={form.legend}>Start from</legend>
        <label className={form.check}>
          <input type="radio" name="start" checked={!copy} onChange={() => setCopy(false)} />A blank costing
        </label>
        <label className={form.check}>
          <input type="radio" name="start" checked={copy} onChange={() => setCopy(true)} />A copy of an
          earlier event for this client
        </label>
      </fieldset>

      {copy &&
        (history.isError ? (
          <Alert tone="danger">
            {problemMessage(history.error, "The client's earlier events didn't load.")}
          </Alert>
        ) : history.isPending ? (
          <p role="status">Finding this client's earlier events…</p>
        ) : earlier.length === 0 ? (
          <p className={form.lead}>This client has no earlier costed events, so start from a blank one.</p>
        ) : (
          <Field label="Copy the costing from">
            {(field) => (
              <select
                {...field}
                value={sourceEventId}
                onChange={(change) => setSourceEventId(change.target.value)}
              >
                <option value="">Choose an event</option>
                {earlier.map((item) => (
                  <option key={item.eventId} value={item.eventId}>
                    {item.name}, {formatDateOnly(item.eventDate)}, {formatMoney(item.totalIncVat!)} incl. VAT
                  </option>
                ))}
              </select>
            )}
          </Field>
        ))}

      <DialogFooter>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          variant="primary"
          busy={start.isPending}
          disabled={waitingForSource}
          onClick={() => void create()}
        >
          {copy ? 'Copy it' : 'Start costing'}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
