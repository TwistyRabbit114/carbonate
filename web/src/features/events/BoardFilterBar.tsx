import { useRef } from 'react';
import { X } from 'lucide-react';
import type { DivisionCode, EventType } from '@/api/types';
import { Button } from '@/components/Button';
import { Field } from '@/components/Field';
import { hasFilters, noFilters, type BoardFilters } from './filters';
import { divisionLabels, eventTypeLabels } from './labels';
import styles from './BoardFilterBar.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type BoardFilterBarProps = {
  filters: BoardFilters;
  shown: number; //events still on the board after filtering
  total: number;
  onChange: (filters: BoardFilters) => void;
};

//code first, so a narrow phone select that cuts the name off still says which division it is
const divisionOptions = Object.entries(divisionLabels).map(([code, label]) => ({
  value: code,
  label: `${code} · ${label}`,
}));

//----------------------------------------------------------\\
//                              COMPONENT
//----------------------------------------------------------\\

//division, event type and a date range, applied as soon as they change
export function BoardFilterBar({ filters, shown, total, onChange }: BoardFilterBarProps) {
  const divisionSelect = useRef<HTMLSelectElement>(null);
  const active = hasFilters(filters);
  const backwards = filters.from !== null && filters.to !== null && filters.from > filters.to;

  const update = (change: Partial<BoardFilters>) => onChange({ ...filters, ...change });

  //the clear button disappears once used, so focus goes to the first filter rather than the page body
  function clear() {
    onChange(noFilters);
    divisionSelect.current?.focus();
  }

  return (
    <div role="search" aria-label="Filter the board" className={styles.bar}>
      <div className={styles.controls}>
        <Field label="Division">
          {(control) => (
            <select
              {...control}
              ref={divisionSelect}
              value={filters.division ?? ''}
              onChange={(e) => update({ division: (e.target.value as DivisionCode) || null })}
            >
              <option value="">All divisions</option>
              {divisionOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field label="Event type">
          {(control) => (
            <select
              {...control}
              value={filters.type ?? ''}
              onChange={(e) => update({ type: (e.target.value as EventType) || null })}
            >
              <option value="">All types</option>
              {Object.entries(eventTypeLabels).map(([type, label]) => (
                <option key={type} value={type}>
                  {label}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field label="From">
          {(control) => (
            <input
              {...control}
              type="date"
              value={filters.from ?? ''}
              max={filters.to ?? undefined}
              onChange={(e) => update({ from: e.target.value || null })}
            />
          )}
        </Field>

        <Field label="To" error={backwards ? 'Pick a date on or after the from date' : undefined}>
          {(control) => (
            <input
              {...control}
              type="date"
              value={filters.to ?? ''}
              min={filters.from ?? undefined}
              onChange={(e) => update({ to: e.target.value || null })}
            />
          )}
        </Field>
      </div>

      <div className={styles.summary}>
        <p className={styles.count} aria-live="polite">
          {active ? `Showing ${shown} of ${total} events` : ''}
        </p>
        {active && (
          <Button variant="ghost" onClick={clear}>
            <X aria-hidden="true" />
            Clear filters
          </Button>
        )}
      </div>
    </div>
  );
}
