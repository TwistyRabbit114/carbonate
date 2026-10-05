import { useState } from 'react';
import { isApiError } from '@/api/problem';
import type {
  EventListItem,
  SaveStockRequirement,
  SourceMode,
  StockItem,
  StockRequirement,
} from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Field } from '@/components/Field';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { useToast } from '@/components/toast/ToastContext';
import { problemMessage } from '@/lib/apiErrors';
import { formatDate, formatDateOnly } from '@/lib/format';
import { useSaveStockRequirements, useStockItems, useStockRequirements } from './api';
import { describeWarning, formatQuantity, sourceLabels } from './labels';
import styles from './StockPage.module.scss';

//----------------------------------------------------------\\
//                              EDITING
//----------------------------------------------------------\\

const sourceModes: SourceMode[] = ['Stock', 'Order', 'Rent'];

//one row of the table while it's being changed. quantity is the box's text until it's saved
type Draft = {
  key: string;
  requirementId: string | null;
  stockItemId: string;
  stockItemName: string;
  unit: string;
  quantity: string;
  requiredByDate: string;
  sourceMode: SourceMode;
  notes: string | null;
};

type RowErrors = Record<string, { quantity?: string; requiredByDate?: string; stockItemId?: string }>;

const draftOf = (line: StockRequirement): Draft => ({
  key: line.requirementId,
  requirementId: line.requirementId,
  stockItemId: line.stockItemId,
  stockItemName: line.stockItemName,
  unit: line.unit,
  quantity: String(line.quantityRequired),
  requiredByDate: line.requiredByDate,
  sourceMode: line.sourceMode,
  notes: line.notes ?? null,
});

//a quantity is a plain number, a comma works as the decimal point too
function quantityOf(text: string) {
  const value = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(value) && value >= 0 ? value : null;
}

function checkRows(rows: Draft[]): RowErrors {
  const errors: RowErrors = {};
  for (const row of rows) {
    const problems: RowErrors[string] = {};
    if (quantityOf(row.quantity) === null) problems.quantity = 'Enter a quantity of 0 or more.';
    if (!row.requiredByDate) problems.requiredByDate = "Choose the date it's needed by.";
    if (Object.keys(problems).length > 0) errors[row.key] = problems;
  }
  return errors;
}

//the api's "items[2].quantityRequired" put back against the row it's about
function rowErrorsFrom(error: unknown, rows: Draft[]): RowErrors {
  if (!isApiError(error) || error.status !== 400) return {};
  const errors: RowErrors = {};
  for (const [key, messages] of Object.entries(error.problem.errors ?? {})) {
    const match = /^items\[(\d+)\]\.(\w+)$/.exec(key);
    const row = match && rows[Number(match[1])];
    if (!row || !messages[0]) continue;
    const field =
      match[2] === 'quantityRequired' ? 'quantity' : (match[2] as 'requiredByDate' | 'stockItemId');
    errors[row.key] = { ...errors[row.key], [field]: messages[0] };
  }
  return errors;
}

//----------------------------------------------------------\\
//                              PANEL
//----------------------------------------------------------\\

type RequirementsPanelProps = {
  events: EventListItem[]; //confirmed and live ones, soonest first
  eventId: string;
  onPickEvent: (eventId: string) => void;
};

//what each confirmed event needs, with the shortfall and lead-time warnings (FR-26, FR-27, FR-29).
//anyone with stock.plan can change it, the whole list is saved in one go
export function RequirementsPanel({ events, eventId, onPickEvent }: RequirementsPanelProps) {
  const event = events.find((candidate) => candidate.eventId === eventId);
  const requirements = useStockRequirements(eventId, true);
  const [editing, setEditing] = useState<Draft[] | null>(null);

  const lines = [...(requirements.data ?? [])].sort(
    (a, b) => b.warnings.length - a.warnings.length || a.stockItemName.localeCompare(b.stockItemName),
  );

  return (
    <Panel
      title="Stock requirements"
      actions={
        !editing &&
        requirements.isSuccess && (
          <Button variant="ghost" onClick={() => setEditing(lines.map(draftOf))}>
            Change quantities
          </Button>
        )
      }
    >
      <div className={styles.picker}>
        <Field label="Event">
          {(field) => (
            <select
              {...field}
              value={eventId}
              disabled={editing !== null}
              onChange={(change) => onPickEvent(change.target.value)}
            >
              {events.map((candidate) => (
                <option key={candidate.eventId} value={candidate.eventId}>
                  {candidate.name}, {formatDate(candidate.startsAt)}
                </option>
              ))}
            </select>
          )}
        </Field>
        {event && (
          <p className={styles.pickerNote}>
            {formatQuantity(event.packSizeActual ?? event.packSizeEstimated)} guests
          </p>
        )}
      </div>

      {requirements.isError ? (
        <ErrorState
          message="We couldn't load the stock for this event."
          onRetry={() => void requirements.refetch()}
        />
      ) : requirements.isPending ? (
        <p role="status">Loading the stock for this event…</p>
      ) : editing ? (
        <RequirementsEditor
          eventId={eventId}
          eventDate={event?.eventDate ?? ''}
          rows={editing}
          onChange={setEditing}
          onDone={() => setEditing(null)}
        />
      ) : lines.length === 0 ? (
        <p>No stock planned for this event yet. Change quantities to add the first line.</p>
      ) : (
        <Table label="Stock for this event">
          <thead>
            <tr>
              <th scope="col">Item</th>
              <NumberCell head>Required</NumberCell>
              <th scope="col">Source</th>
              <th scope="col">Needed by</th>
              <th scope="col">Status</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => (
              <tr key={line.requirementId}>
                <th scope="row">{line.stockItemName}</th>
                <NumberCell>
                  {formatQuantity(line.quantityRequired)} {line.unit}
                </NumberCell>
                <td>{sourceLabels[line.sourceMode]}</td>
                <td className={styles.nowrap}>{formatDateOnly(line.requiredByDate)}</td>
                <td>
                  {line.warnings.length === 0 ? (
                    'OK'
                  ) : (
                    <ul className={styles.warnings}>
                      {line.warnings.map((warning) => (
                        <li key={warning.code}>{describeWarning(warning, line.unit)}</li>
                      ))}
                    </ul>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Panel>
  );
}

//----------------------------------------------------------\\
//                              EDITOR
//----------------------------------------------------------\\

type RequirementsEditorProps = {
  eventId: string;
  eventDate: string;
  rows: Draft[];
  onChange: (rows: Draft[]) => void;
  onDone: () => void;
};

function RequirementsEditor({ eventId, eventDate, rows, onChange, onDone }: RequirementsEditorProps) {
  const items = useStockItems();
  const save = useSaveStockRequirements(eventId);
  const toast = useToast();
  const [errors, setErrors] = useState<RowErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [adding, setAdding] = useState('');

  const update = (key: string, change: Partial<Draft>) =>
    onChange(rows.map((row) => (row.key === key ? { ...row, ...change } : row)));
  const unused = (items.data ?? []).filter(
    (item) => !item.isAsset && !rows.some((row) => row.stockItemId === item.stockItemId),
  );

  function add(item: StockItem) {
    onChange([
      ...rows,
      {
        key: `new-${item.stockItemId}`,
        requirementId: null,
        stockItemId: item.stockItemId,
        stockItemName: item.name,
        unit: item.unit,
        quantity: '',
        requiredByDate: eventDate,
        sourceMode: 'Stock',
        notes: null,
      },
    ]);
    setAdding('');
  }

  async function onSave() {
    setServerError(null);
    const found = checkRows(rows);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const request: SaveStockRequirement[] = rows.map((row) => ({
      requirementId: row.requirementId,
      stockItemId: row.stockItemId,
      quantityRequired: quantityOf(row.quantity)!,
      requiredByDate: row.requiredByDate,
      sourceMode: row.sourceMode,
      notes: row.notes,
    }));
    try {
      await save.mutateAsync(request);
      toast.success('The stock list is saved.');
      onDone();
    } catch (error) {
      const fromApi = rowErrorsFrom(error, rows);
      setErrors(fromApi);
      if (Object.keys(fromApi).length === 0) {
        setServerError(
          isApiError(error) && error.status === 400 && error.problem.errors?.items?.[0]
            ? error.problem.errors.items[0]
            : problemMessage(error, "The stock list wasn't saved."),
        );
      }
    }
  }

  return (
    <>
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <Table label="Change the stock for this event">
        <thead>
          <tr>
            <th scope="col">Item</th>
            <th scope="col">Required</th>
            <th scope="col">Source</th>
            <th scope="col">Needed by</th>
            <th scope="col">
              <span className={styles.hidden}>Remove</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key}>
              <th scope="row">{row.stockItemName}</th>
              <td>
                <span className={styles.cellField}>
                  <input
                    className={`${styles.cellInput} ${styles.quantity}`}
                    aria-label={`${row.stockItemName}, quantity in ${row.unit}`}
                    aria-invalid={errors[row.key]?.quantity ? true : undefined}
                    inputMode="decimal"
                    value={row.quantity}
                    onChange={(change) => update(row.key, { quantity: change.target.value })}
                  />
                  {row.unit}
                </span>
                {errors[row.key]?.quantity && (
                  <span className={styles.cellError}>{errors[row.key]!.quantity}</span>
                )}
              </td>
              <td>
                <select
                  className={styles.cellInput}
                  aria-label={`${row.stockItemName}, source`}
                  value={row.sourceMode}
                  onChange={(change) => update(row.key, { sourceMode: change.target.value as SourceMode })}
                >
                  {sourceModes.map((mode) => (
                    <option key={mode} value={mode}>
                      {sourceLabels[mode]}
                    </option>
                  ))}
                </select>
              </td>
              <td>
                <input
                  type="date"
                  className={styles.cellInput}
                  aria-label={`${row.stockItemName}, needed by`}
                  aria-invalid={errors[row.key]?.requiredByDate ? true : undefined}
                  value={row.requiredByDate}
                  onChange={(change) => update(row.key, { requiredByDate: change.target.value })}
                />
                {errors[row.key]?.requiredByDate && (
                  <span className={styles.cellError}>{errors[row.key]!.requiredByDate}</span>
                )}
              </td>
              <td>
                <Button
                  variant="ghost"
                  aria-label={`Take ${row.stockItemName} off the list`}
                  onClick={() => onChange(rows.filter((candidate) => candidate.key !== row.key))}
                >
                  Remove
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </Table>

      <div className={styles.editorFoot}>
        <Field label="Add an item">
          {(field) => (
            <select
              {...field}
              value={adding}
              disabled={unused.length === 0}
              onChange={(change) => {
                const item = unused.find((candidate) => candidate.stockItemId === change.target.value);
                if (item) add(item);
              }}
            >
              <option value="">{unused.length === 0 ? 'Everything is on the list' : 'Choose an item'}</option>
              {unused.map((item) => (
                <option key={item.stockItemId} value={item.stockItemId}>
                  {item.name} ({item.unit})
                </option>
              ))}
            </select>
          )}
        </Field>
        <div className={styles.editorButtons}>
          <Button onClick={onDone}>Cancel</Button>
          <Button variant="primary" busy={save.isPending} onClick={() => void onSave()}>
            Save stock list
          </Button>
        </div>
      </div>
      {rows.some((row) => row.requirementId === null) && (
        <p className={styles.hint}>
          New lines start as from stock, needed by the event date. Change either before saving.
        </p>
      )}
    </>
  );
}
