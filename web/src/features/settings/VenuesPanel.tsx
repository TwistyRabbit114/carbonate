import { useState } from 'react';
import type { Venue } from '@/api/types';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { Table } from '@/components/Table';
import { useAllVenues } from '@/features/venues/api';
import { openingHours } from '@/features/venues/places';
import { VenueDialog } from '@/features/venues/VenueDialog';
import styles from './SettingsPage.module.scss';

//the venues events pick from (FR-32), for anyone with venue.edit
export function VenuesPanel() {
  const venues = useAllVenues();
  //undefined is closed, null is a new venue
  const [editing, setEditing] = useState<Venue | null | undefined>(undefined);

  return (
    <Panel
      title="Venues"
      actions={
        <Button variant="ghost" onClick={() => setEditing(null)}>
          Add venue
        </Button>
      }
    >
      {venues.isError ? (
        <ErrorState message="We couldn't load the venues." onRetry={() => void venues.refetch()} />
      ) : venues.isPending ? (
        <p role="status">Loading the venues…</p>
      ) : venues.data.length === 0 ? (
        <p>No venues yet. Add the first one, and events can pick it from then on.</p>
      ) : (
        <Table label="Venue list">
          <thead>
            <tr>
              <th scope="col">Venue</th>
              <th scope="col">Open</th>
              <th scope="col">Needs</th>
              <th scope="col">
                <span className={styles.hidden}>Change</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {venues.data.map((venue) => (
              <tr key={venue.venueId}>
                <th scope="row">
                  {venue.name} {!venue.isActive && <Badge>Not in use</Badge>}
                  <span className={styles.sub}>{venue.address}</span>
                </th>
                <td>{openingHours(venue) ?? 'Not set'}</td>
                <td>
                  {[
                    venue.requiresSecurityClearance && 'Security clearance',
                    venue.requiresHealthSafetyFile && 'H&S file',
                  ]
                    .filter(Boolean)
                    .join(', ') || 'Nothing extra'}
                </td>
                <td>
                  <Button variant="ghost" onClick={() => setEditing(venue)} aria-label={`Edit ${venue.name}`}>
                    Edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {editing !== undefined && <VenueDialog venue={editing} onClose={() => setEditing(undefined)} />}
    </Panel>
  );
}
