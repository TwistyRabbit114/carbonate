import { useState } from 'react';
import { isApiError } from '@/api/problem';
import type { Quote, QuoteLineCategory, SaveQuoteLine } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { Table } from '@/components/Table';
import { useToast } from '@/components/toast/ToastContext';
import { isConflict, problemMessage } from '@/lib/apiErrors';
import { parseRand } from '@/lib/format';
import { useSaveQuote } from './api';
import { categoryLabels, quoteCategories } from './labels';
import styles from './QuotePage.module.scss';

//----------------------------------------------------------\\
//                              ROWS
//----------------------------------------------------------\\

//one line while it's being typed. the boxes hold text until it's saved
type Row = {
  key: string;
  category: QuoteLineCategory;
  description: string;
  quantity: string;
  cost: string;
  price: string;
};

type RowErrors = Record<string, Partial<Record<'description' | 'quantity' | 'cost' | 'price', string>>>;

let rowCounter = 0;
const newKey = () => `row-${++rowCounter}`;

//amounts back into the box with a decimal comma, the way they're typed
const randText = (value: number | null | undefined) => (value == null ? '' : String(value).replace('.', ','));

function rowsFrom(quote: Quote): Row[] {
  return quote.lines.map((line) => ({
    key: newKey(),
    category: line.category,
    description: line.description,
    quantity: String(line.quantity).replace('.', ','),
    cost: randText(line.unitCostToUs),
    price: randText(line.unitPriceToClient),
  }));
}

const amountLabels = {
  quantity: 'quantity',
  cost: 'unit cost to us',
  price: 'unit price to client',
} as const;

const blankRow = (): Row => ({
  key: newKey(),
  category: 'Other',
  description: '',
  quantity: '1',
  cost: '',
  price: '',
});

function quantityOf(text: string) {
  const value = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(value) && value > 0 ? value : null;
}

function check(rows: Row[]): RowErrors {
  const errors: RowErrors = {};
  for (const row of rows) {
    const problems: RowErrors[string] = {};
    if (!row.description.trim()) problems.description = 'Describe the line.';
    else if (row.description.trim().length > 500) problems.description = 'Keep it under 500 characters.';
    if (quantityOf(row.quantity) === null) problems.quantity = 'More than 0.';
    if (parseRand(row.cost) === null) problems.cost = 'In rand, like 4 200.';
    if (parseRand(row.price) === null) problems.price = 'In rand, like 6 500.';
    if (Object.keys(problems).length > 0) errors[row.key] = problems;
  }
  return errors;
}

const fieldOf: Record<string, keyof RowErrors[string]> = {
  description: 'description',
  quantity: 'quantity',
  unitCostToUs: 'cost',
  unitPriceToClient: 'price',
};

//the api's "lines[2].quantity" back against the row it's about
function rowErrorsFrom(error: unknown, rows: Row[]): RowErrors {
  if (!isApiError(error) || error.status !== 400) return {};
  const errors: RowErrors = {};
  for (const [key, messages] of Object.entries(error.problem.errors ?? {})) {
    const match = /^lines\[(\d+)\]\.(\w+)$/.exec(key);
    const row = match && rows[Number(match[1])];
    const field = match && fieldOf[match[2]!];
    if (row && field && messages[0]) errors[row.key] = { ...errors[row.key], [field]: messages[0] };
  }
  return errors;
}

//----------------------------------------------------------\\
//                              EDITOR
//----------------------------------------------------------\\

type QuoteLinesEditorProps = {
  quote: Quote;
  onSaved: (saved: Quote) => void;
  onCancel: () => void;
  onReload: () => void; //after a conflict, fetch what's there now
};

//the costing's lines with what each costs carbon and what the client pays (FR-11). totals, vat and
//margin are worked out by the api when it's saved, never here
export function QuoteLinesEditor({ quote, onSaved, onCancel, onReload }: QuoteLinesEditorProps) {
  const save = useSaveQuote(quote.quoteId);
  const toast = useToast();
  const [rows, setRows] = useState<Row[]>(() => (quote.lines.length > 0 ? rowsFrom(quote) : [blankRow()]));
  const [validUntil, setValidUntil] = useState(quote.validUntil ?? '');
  const [errors, setErrors] = useState<RowErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const update = (key: string, change: Partial<Row>) =>
    setRows((current) => current.map((row) => (row.key === key ? { ...row, ...change } : row)));

  async function onSave() {
    setServerError(null);
    setConflict(false);
    const found = check(rows);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const lines: SaveQuoteLine[] = rows.map((row) => ({
      category: row.category,
      description: row.description.trim(),
      quantity: quantityOf(row.quantity)!,
      unitCostToUs: parseRand(row.cost),
      unitPriceToClient: parseRand(row.price),
    }));
    try {
      const saved = await save.mutateAsync({
        lines,
        validUntil: validUntil || null,
        rowVersion: quote.rowVersion,
      });
      toast.success(
        saved.quoteId === quote.quoteId
          ? 'The costing is saved.'
          : `Version ${saved.version} is saved, the one sent to the client is kept as it was.`,
      );
      onSaved(saved);
    } catch (error) {
      if (isConflict(error)) {
        setConflict(true);
        return;
      }
      const fromApi = rowErrorsFrom(error, rows);
      setErrors(fromApi);
      if (Object.keys(fromApi).length === 0)
        setServerError(problemMessage(error, "The costing wasn't saved."));
    }
  }

  const cell = (row: Row, field: keyof RowErrors[string]) => errors[row.key]?.[field];

  return (
    <>
      {conflict && (
        <div className={styles.alert}>
          <Alert tone="warning">
            <p className={styles.alertText}>
              Someone else changed this costing while you were working on it. Load their version, then make
              your changes again.
            </p>
            <Button onClick={onReload}>Load their version</Button>
          </Alert>
        </div>
      )}
      {serverError && (
        <div className={styles.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      {quote.status === 'Issued' && (
        <p className={styles.lead}>
          This was sent to the client, so saving makes version {quote.version + 1}. What they were sent stays
          as it is.
        </p>
      )}
      {(quote.status === 'Approved' || quote.status === 'PendingApproval') && (
        <p className={styles.lead}>Saving takes it back to draft, the approval doesn't cover new numbers.</p>
      )}

      <Table label="Change the costing lines">
        <thead>
          <tr>
            <th scope="col">Category</th>
            <th scope="col">Description</th>
            <th scope="col">Quantity</th>
            <th scope="col">Unit cost to us</th>
            <th scope="col">Unit price to client</th>
            <th scope="col">
              <span className={styles.hidden}>Remove</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => {
            const line = `Line ${index + 1}`;
            return (
              <tr key={row.key}>
                <td>
                  <select
                    className={styles.cellInput}
                    aria-label={`${line} category`}
                    value={row.category}
                    onChange={(change) =>
                      update(row.key, { category: change.target.value as QuoteLineCategory })
                    }
                  >
                    {quoteCategories.map((category) => (
                      <option key={category} value={category}>
                        {categoryLabels[category]}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <input
                    className={`${styles.cellInput} ${styles.description}`}
                    aria-label={`${line} description`}
                    aria-invalid={cell(row, 'description') ? true : undefined}
                    maxLength={500}
                    value={row.description}
                    onChange={(change) => update(row.key, { description: change.target.value })}
                  />
                  {cell(row, 'description') && (
                    <span className={styles.cellError}>{cell(row, 'description')}</span>
                  )}
                </td>
                {(['quantity', 'cost', 'price'] as const).map((field) => (
                  <td key={field}>
                    <input
                      className={`${styles.cellInput} ${styles.amount}`}
                      aria-label={`${line} ${amountLabels[field]}`}
                      aria-invalid={cell(row, field) ? true : undefined}
                      inputMode="decimal"
                      value={row[field]}
                      onChange={(change) => update(row.key, { [field]: change.target.value })}
                    />
                    {cell(row, field) && <span className={styles.cellError}>{cell(row, field)}</span>}
                  </td>
                ))}
                <td>
                  <Button
                    variant="ghost"
                    aria-label={`Remove ${line.toLowerCase()}`}
                    onClick={() =>
                      setRows((current) => current.filter((candidate) => candidate.key !== row.key))
                    }
                  >
                    Remove
                  </Button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </Table>

      <div className={styles.editorFoot}>
        <div className={styles.editorExtras}>
          <Button onClick={() => setRows((current) => [...current, blankRow()])}>Add a line</Button>
          <Field label="Valid until">
            {(field) => (
              <input
                {...field}
                type="date"
                value={validUntil}
                onChange={(change) => setValidUntil(change.target.value)}
              />
            )}
          </Field>
        </div>
        <div className={styles.editorButtons}>
          <Button onClick={onCancel}>Cancel</Button>
          <Button variant="primary" busy={save.isPending} onClick={() => void onSave()}>
            {quote.status === 'Issued' ? `Save as version ${quote.version + 1}` : 'Save costing'}
          </Button>
        </div>
      </div>
      <p className={styles.hint}>Totals, VAT and the margin are worked out when you save.</p>
    </>
  );
}
