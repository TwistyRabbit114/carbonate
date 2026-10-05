import { useState } from 'react';
import type { StockItem } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { formatMoney } from '@/lib/format';
import { useCatalogueItems, useSuppliers } from './api';
import { formatQuantity } from './labels';
import { StockItemDialog } from './StockItemDialog';
import styles from './StockPage.module.scss';

//everything carbon stocks, orders or rents (FR-24). stock.manage adds and changes items
export function ItemsPanel() {
  const canManage = usePermissions().can('stock.manage');
  const items = useCatalogueItems();
  const suppliers = useSuppliers();
  //undefined is closed, null is a new item
  const [editing, setEditing] = useState<StockItem | null | undefined>(undefined);
  const supplierName = (supplierId: string | null | undefined) =>
    suppliers.data?.find((supplier) => supplier.supplierId === supplierId)?.name;
  //$cost: a role that can't see it gets no column at all (NFR-17)
  const showsCost = items.data?.some((item) => item.standardUnitCost !== undefined) ?? false;

  return (
    <Panel
      title="Stock items"
      actions={
        canManage && (
          <Button variant="ghost" onClick={() => setEditing(null)}>
            Add item
          </Button>
        )
      }
    >
      {items.isError ? (
        <ErrorState message="We couldn't load the catalogue." onRetry={() => void items.refetch()} />
      ) : items.isPending ? (
        <p role="status">Loading the catalogue…</p>
      ) : items.data.length === 0 ? (
        <p>The catalogue is empty.</p>
      ) : (
        <Table label="Catalogue">
          <thead>
            <tr>
              <th scope="col">Item</th>
              <th scope="col">Category</th>
              <NumberCell head>Per 100 guests</NumberCell>
              <th scope="col">Supplier</th>
              {showsCost && <NumberCell head>Standard cost</NumberCell>}
              {canManage && (
                <th scope="col">
                  <span className={styles.hidden}>Change</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {items.data.map((item) => (
              <tr key={item.stockItemId}>
                <th scope="row">
                  {item.name} {!item.isActive && <Badge>Not in use</Badge>}
                  <span className={styles.sub}>
                    {item.sku}, counted in {item.unit}
                  </span>
                </th>
                <td>{item.categoryName}</td>
                <NumberCell>
                  {item.consumptionPerHundredGuests != null &&
                    formatQuantity(item.consumptionPerHundredGuests)}
                </NumberCell>
                <td>
                  {supplierName(item.defaultSupplierId) ?? (item.defaultSupplierId ? '' : 'We hold it')}
                </td>
                {showsCost && (
                  <NumberCell>
                    {item.standardUnitCost != null && formatMoney(item.standardUnitCost)}
                  </NumberCell>
                )}
                {canManage && (
                  <td>
                    <Button variant="ghost" onClick={() => setEditing(item)} aria-label={`Edit ${item.name}`}>
                      Edit
                    </Button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {editing !== undefined && <StockItemDialog item={editing} onClose={() => setEditing(undefined)} />}
    </Panel>
  );
}
