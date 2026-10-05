import { Badge } from '@/components/Badge';
import { FieldList, FieldRow } from '@/components/FieldList';
import { Item, ItemList } from '@/components/ItemList';
import { Panel } from '@/components/Panel';
import { loadLabels, readServiceSchedule } from './serviceSchedule';
import styles from './ServiceSchedulePanel.module.scss';

type ServiceSchedulePanelProps = {
  schedule: unknown; //the event's serviceSchedule, json as stored
  crew?: boolean; //crew get told plainly when the bars will be packed
};

//the run of the night (FR-05). nothing at all when the event has no schedule yet
//TODO(plan): editing the schedule waits on PUT /api/events/{id}/service-schedule, still a stub
export function ServiceSchedulePanel({ schedule, crew = false }: ServiceSchedulePanelProps) {
  const read = readServiceSchedule(schedule);
  if (!read) return null;

  return (
    <Panel title="Service schedule">
      <FieldList>
        <FieldRow label="Doors open">{read.doorsOpen.slice(0, 5)}</FieldRow>
        <FieldRow label="Doors close">{read.doorsClose?.slice(0, 5)}</FieldRow>
        <FieldRow label="Dinner service">{read.dinnerServiceAt?.slice(0, 5)}</FieldRow>
      </FieldList>

      <h3 className={styles.subhead}>Through the night</h3>
      <ItemList label="Service windows">
        {read.serviceWindows.map((window) => (
          <Item
            key={`${window.start}-${window.label}`}
            title={`${window.start.slice(0, 5)} to ${window.end.slice(0, 5)} · ${window.label}`}
            badge={
              <Badge tone={window.expectedLoad === 'peak' ? 'warning' : 'neutral'}>
                {loadLabels[window.expectedLoad]}
              </Badge>
            }
            meta={windowMeta(window.barsOpen, window.staffOnShift, crew && window.expectedLoad === 'peak')}
          />
        ))}
      </ItemList>

      {read.layoutNotes && <p>{read.layoutNotes}</p>}
    </Panel>
  );
}

function windowMeta(bars: number | undefined, staff: number | undefined, packed: boolean) {
  const parts = [
    packed ? 'Bars will be packed' : undefined,
    bars === undefined ? undefined : `${bars} ${bars === 1 ? 'bar' : 'bars'} open`,
    staff === undefined ? undefined : `${staff} staff on`,
  ].filter(Boolean);
  return parts.length > 0 ? parts.join(' · ') : undefined;
}
