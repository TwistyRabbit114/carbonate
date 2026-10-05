import type { AuditEntry } from '@/api/types';

//the audit trail's before and after json, read back as a few plain lines (FR-37). it's only ever
//rendered as text, so whatever is in it can't become markup

export type AuditChange = { field: string; before: string | null; after: string | null };

//"packSizeEstimated" reads as "Pack size estimated"
export function fieldLabel(key: string) {
  const words = key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_.]/g, ' ')
    .toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

//"event.updated" reads as "Event updated"
export const actionLabel = (action: string) => fieldLabel(action);

function show(value: unknown): string {
  if (value === null || value === undefined || value === '') return 'nothing';
  if (typeof value === 'boolean') return value ? 'yes' : 'no';
  if (Array.isArray(value)) return value.map(show).join(', ');
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function parse(json: string | null | undefined): Record<string, unknown> | null {
  if (!json) return null;
  try {
    const value: unknown = JSON.parse(json);
    return value && typeof value === 'object' && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : { value };
  } catch {
    return { value: json };
  }
}

//only the fields that differ. a new record lists what it was created with, a removed one what it had
export function auditChanges(entry: Pick<AuditEntry, 'beforeJson' | 'afterJson'>): AuditChange[] {
  const before = parse(entry.beforeJson);
  const after = parse(entry.afterJson);
  const keys = [...new Set([...Object.keys(before ?? {}), ...Object.keys(after ?? {})])];

  return keys
    .filter((key) => JSON.stringify(before?.[key]) !== JSON.stringify(after?.[key]))
    .map((key) => ({
      field: fieldLabel(key),
      before: before ? show(before[key]) : null,
      after: after ? show(after[key]) : null,
    }));
}
