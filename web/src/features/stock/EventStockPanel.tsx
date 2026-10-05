import type { StockRequirement } from '@/api/types';
import { Badge } from '@/components/Badge';
import { ErrorState } from '@/components/ErrorState';
import { Item, ItemList } from '@/components/ItemList';
import { LinkButton } from '@/components/LinkButton';
import { Panel } from '@/components/Panel';
import { formatDateOnly } from '@/lib/format';
import { useStockRequirements } from './api';
import { describeWarning, formatQuantity, sourceLabels } from './labels';
import styles from './EventStockPanel.module.scss';

//----------------------------------------------------------\\
//                              PANEL
//----------------------------------------------------------\\

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
    <Panel
      title="Stock"
      actions={<LinkButton to={`/stock?event=${eventId}`}>Open in Stock & Orders</LinkButton>}
    >
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
      meta={`${formatQuantity(line.quantityRequired)} ${line.unit} · ${sourceLabels[line.sourceMode]} · by ${formatDateOnly(line.requiredByDate)}`}
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
