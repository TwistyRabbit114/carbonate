import { useState } from 'react';
import type { AuditEntry } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Field } from '@/components/Field';
import { Panel } from '@/components/Panel';
import { Table } from '@/components/Table';
import { problemMessage } from '@/lib/apiErrors';
import { formatDateTime, formatCount } from '@/lib/format';
import { auditPageSize, useAudit, type AuditFilters } from './api';
import { actionLabel, auditChanges } from './auditChanges';
import styles from './SettingsPage.module.scss';

//----------------------------------------------------------\\
//                              FILTERS
//----------------------------------------------------------\\

//the records the api writes audit entries for, by the entity name it uses
const entities: [string, string][] = [
  ['Event', 'Events'],
  ['Quote', 'Costings'],
  ['Invoice', 'Invoices'],
  ['TaskCard', 'Task cards'],
  ['OrderList', 'Order lists'],
  ['StockItem', 'Stock items'],
  ['Supplier', 'Suppliers'],
  ['EquipmentAsset', 'Equipment'],
  ['IncidentReport', 'Incidents'],
  ['Venue', 'Venues'],
  ['SiteVisit', 'Recces'],
  ['AppUser', 'Users'],
];

const entityLabel = (name: string) => entities.find(([value]) => value === name)?.[1] ?? name;

//----------------------------------------------------------\\
//                              PANEL
//----------------------------------------------------------\\

//every change anyone made, read-only and director only (FR-37, NFR-16). the trail itself can't
//be edited, by anyone, and passwords and secrets never go into it
export function AuditPanel() {
  const [filters, setFilters] = useState<AuditFilters>({ entity: '', from: '', to: '', page: 1 });
  const audit = useAudit(filters);
  const filter = (change: Partial<AuditFilters>) =>
    setFilters((current) => ({ ...current, ...change, page: 1 }));
  const pages = audit.data ? Math.max(1, Math.ceil(audit.data.total / auditPageSize)) : 1;

  return (
    <Panel title="Audit log">
      <div className={styles.filters}>
        <Field label="Record">
          {(field) => (
            <select
              {...field}
              value={filters.entity}
              onChange={(event) => filter({ entity: event.target.value })}
            >
              <option value="">Everything</option>
              {entities.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          )}
        </Field>
        <Field label="From">
          {(field) => (
            <input
              {...field}
              type="date"
              value={filters.from}
              onChange={(event) => filter({ from: event.target.value })}
            />
          )}
        </Field>
        <Field label="To">
          {(field) => (
            <input
              {...field}
              type="date"
              value={filters.to}
              onChange={(event) => filter({ to: event.target.value })}
            />
          )}
        </Field>
      </div>

      {audit.isError ? (
        <ErrorState
          message={problemMessage(audit.error, "The audit log didn't load.")}
          onRetry={() => void audit.refetch()}
        />
      ) : audit.isPending ? (
        <p role="status">Loading the audit log…</p>
      ) : audit.data.items.length === 0 ? (
        <p>Nothing was changed in that range.</p>
      ) : (
        <>
          <Table label="Audit entries">
            <thead>
              <tr>
                <th scope="col">When</th>
                <th scope="col">Who</th>
                <th scope="col">What</th>
                <th scope="col">Changes</th>
              </tr>
            </thead>
            <tbody>
              {audit.data.items.map((entry) => (
                <AuditRow key={entry.auditId} entry={entry} />
              ))}
            </tbody>
          </Table>

          <div className={styles.pager}>
            <p role="status">
              Page {filters.page} of {pages}, {formatCount(audit.data.total)} entries
            </p>
            <Button
              disabled={filters.page <= 1}
              onClick={() => setFilters((current) => ({ ...current, page: current.page - 1 }))}
            >
              Newer
            </Button>
            <Button
              disabled={filters.page >= pages}
              onClick={() => setFilters((current) => ({ ...current, page: current.page + 1 }))}
            >
              Older
            </Button>
          </div>
        </>
      )}
    </Panel>
  );
}

function AuditRow({ entry }: { entry: AuditEntry }) {
  const changes = auditChanges(entry);

  return (
    <tr>
      <td className={styles.nowrap}>{formatDateTime(entry.occurredAt)}</td>
      <td>{entry.userName ?? 'Carbonate, automatically'}</td>
      <td>
        {actionLabel(entry.action)}
        <span className={styles.sub}>{entityLabel(entry.entityName)}</span>
      </td>
      <td>
        {changes.length === 0 ? (
          <span className={styles.sub}>No field changes recorded</span>
        ) : (
          <details>
            <summary className={styles.toggle}>
              {changes.length === 1 ? '1 change' : `${changes.length} changes`}
            </summary>
            <ul className={styles.changes}>
              {changes.map((change) => (
                <li key={change.field}>
                  <strong>{change.field}</strong>{' '}
                  {change.before === null ? (
                    <>set to {change.after}</>
                  ) : change.after === null ? (
                    <>was {change.before}, removed</>
                  ) : (
                    <>
                      {change.before} to {change.after}
                    </>
                  )}
                </li>
              ))}
            </ul>
          </details>
        )}
      </td>
    </tr>
  );
}
