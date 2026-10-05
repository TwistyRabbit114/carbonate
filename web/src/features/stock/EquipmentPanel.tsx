import { useState } from 'react';
import type { EquipmentAsset } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { Table } from '@/components/Table';
import { formatDateOnly } from '@/lib/format';
import { useCatalogueItems, useEquipment } from './api';
import { EquipmentDialog } from './EquipmentDialog';
import styles from './StockPage.module.scss';

//each serialised piece of kit, so a breakdown is pinned to the machine it happened to (FR-31)
export function EquipmentPanel() {
  const canManage = usePermissions().can('stock.manage');
  const equipment = useEquipment();
  const items = useCatalogueItems();
  //undefined is closed, null is a new asset
  const [editing, setEditing] = useState<EquipmentAsset | null | undefined>(undefined);
  const kinds = (items.data ?? []).filter((item) => item.isAsset);
  const assets = [...(equipment.data ?? [])].sort(
    (a, b) => a.stockItemName.localeCompare(b.stockItemName) || a.serialNumber.localeCompare(b.serialNumber),
  );

  return (
    <Panel
      title="Equipment"
      actions={
        canManage && (
          <Button variant="ghost" onClick={() => setEditing(null)}>
            Add equipment
          </Button>
        )
      }
    >
      {equipment.isError ? (
        <ErrorState message="We couldn't load the equipment." onRetry={() => void equipment.refetch()} />
      ) : equipment.isPending ? (
        <p role="status">Loading the equipment…</p>
      ) : assets.length === 0 ? (
        <p>No serialised equipment recorded.</p>
      ) : (
        <Table label="Equipment list">
          <thead>
            <tr>
              <th scope="col">Serial number</th>
              <th scope="col">Kind</th>
              <th scope="col">Condition</th>
              <th scope="col">Status</th>
              <th scope="col">Bought</th>
              {canManage && (
                <th scope="col">
                  <span className={styles.hidden}>Change</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {assets.map((asset) => (
              <tr key={asset.assetId}>
                <th scope="row">{asset.serialNumber}</th>
                <td>{asset.stockItemName}</td>
                <td>{asset.condition}</td>
                <td>{asset.status}</td>
                <td className={styles.nowrap}>{asset.purchaseDate && formatDateOnly(asset.purchaseDate)}</td>
                {canManage && (
                  <td>
                    <Button
                      variant="ghost"
                      onClick={() => setEditing(asset)}
                      aria-label={`Edit ${asset.serialNumber}`}
                    >
                      Edit
                    </Button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {editing !== undefined && (
        <EquipmentDialog asset={editing} kinds={kinds} onClose={() => setEditing(undefined)} />
      )}
    </Panel>
  );
}
