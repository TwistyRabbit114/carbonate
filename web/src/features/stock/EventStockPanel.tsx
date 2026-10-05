import type { SourceMode, StockRequirement, StockWarning } from '@/api/types';
import { Badge } from '@/components/Badge';
import { ErrorState } from '@/components/ErrorState';
import { Item, ItemList } from '@/components/ItemList';
import { LinkButton } from '@/components/LinkButton';
import { Panel } from '@/components/Panel';
import { formatDateOnly } from '@/lib/format';
import { useStockRequirements } from './api';
import styles from './EventStockPanel.module.scss';

//----------------------------------------------------------\
//                              WORDING
//----------------------------------------------------------\

const sourceLabels: Record<SourceMode, string> = {
  Stock: 'From stock',
  Order: 'To order',
  Rent: 'To rent',
};

const quantity = new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 2 });

//the warnings in plain words (FR-27, FR-29). anything new from the api still gets a line
function describeWarning(warning: StockWarning, unit: string) {
  if (warning.code === 'SHORTFALL' && warning.planned != null && warning.expected != null) {
    return `Below normal use for the pack size: ${quantity.format(warning.planned)} ${unit} planned, about ${quantity.format(warning.expected)} ${unit} expected.`;
  }
  if (warning.code === 'LEAD_TIME' && warning.leadTimeDays != null && warning.requiredBy) {
    return `The supplier needs ${warning.leadTimeDays} days' notice and it's wanted by ${formatDateOnly(warning.requiredBy)}.`;
  }
  return 'Check this line before ordering.';
}

//----------------------------------------------------------\
//                              PANEL
//----------------------------------------------------------\

type EventStockPanelProps = {
  eventId: string;
  canPlan: boolean; //D's api takes stock.plan to read these
};

//what the event needs, set up from its template and the pack size (FR-25, FR-26). quantities only
export function EventStockPanel({ eventId, canPlan }: EventStockPanelProps) {
  const requirements = useStockRequirements(eventId, canPlan);
  if (!canPlan) return null;

  const lines = [...(requirements.data ?? [])].sort(
    (a, b) => b.warnings.length - a.warnings.length || a.stockItemName.localeCompare(b.stockItemName),
  );

  return (
    <Panel title="Stock" actions={<LinkButton to="/stock">Open in Stock & Orders</LinkButton>}>
      {requirements.isError ? (
        <ErrorState message="We couldn't load the stock list." onRetry={() => void requirements.refetch()} />
      ) : requirements.isPending ? (
        <p role="status">Loading the stock list…</p>
      ) : lines.length === 0 ? (
        <p>No stock planned for this event yet.</p>
      ) : (
        <ItemList label="Stock for this event">
          {lines.map((line) => (
            <StockLine key={line.requirementId} line={line} />
          ))}
        </ItemList>
      )}
    </Panel>
  );
}

function StockLine({ line }: { line: StockRequirement }) {
  return (
    <Item
      title={line.stockItemName}
      badge={line.warnings.length > 0 && <Badge tone="warning">Check</Badge>}
      meta={`${quantity.format(line.quantityRequired)} ${line.unit} · ${sourceLabels[line.sourceMode]} · by ${formatDateOnly(line.requiredByDate)}`}
    >
      {line.warnings.length > 0 && (
        <ul className={styles.warnings}>
          {line.warnings.map((warning) => (
            <li key={warning.code}>{describeWarning(warning, line.unit)}</li>
          ))}
        </ul>
      )}
    </Item>
  );
}
