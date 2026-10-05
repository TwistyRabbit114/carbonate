import { useState } from 'react';
import type { Supplier } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { useSuppliers } from './api';
import { SupplierDialog } from './SupplierDialog';
import styles from './StockPage.module.scss';

//who carbon orders and rents from, with the notice each needs (FR-28, FR-29)
export function SuppliersPanel() {
  const canManage = usePermissions().can('stock.manage');
  const suppliers = useSuppliers();
  //undefined is closed, null is a new supplier
  const [editing, setEditing] = useState<Supplier | null | undefined>(undefined);

  return (
    <Panel
      title="Suppliers"
      actions={
        canManage && (
          <Button variant="ghost" onClick={() => setEditing(null)}>
            Add supplier
          </Button>
        )
      }
    >
      {suppliers.isError ? (
        <ErrorState message="We couldn't load the suppliers." onRetry={() => void suppliers.refetch()} />
      ) : suppliers.isPending ? (
        <p role="status">Loading the suppliers…</p>
      ) : suppliers.data.length === 0 ? (
        <p>No suppliers yet.</p>
      ) : (
        <Table label="Supplier list">
          <thead>
            <tr>
              <th scope="col">Supplier</th>
              <NumberCell head>Lead time</NumberCell>
              <th scope="col">Contact</th>
              {canManage && (
                <th scope="col">
                  <span className={styles.hidden}>Change</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {suppliers.data.map((supplier) => (
              <tr key={supplier.supplierId}>
                <th scope="row">
                  {supplier.name} {supplier.isLiquorSupplier && <Badge>Liquor</Badge>}{' '}
                  {!supplier.isActive && <Badge>Not used</Badge>}
                </th>
                <NumberCell>
                  {supplier.leadTimeDays === 1 ? '1 day' : `${supplier.leadTimeDays} days`}
                </NumberCell>
                <td>
                  {supplier.contactName}
                  {supplier.email && (
                    <span className={styles.sub}>
                      <a href={`mailto:${supplier.email}`}>{supplier.email}</a>
                    </span>
                  )}
                  {supplier.phone && <span className={styles.sub}>{supplier.phone}</span>}
                </td>
                {canManage && (
                  <td>
                    <Button
                      variant="ghost"
                      onClick={() => setEditing(supplier)}
                      aria-label={`Edit ${supplier.name}`}
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

      {editing !== undefined && <SupplierDialog supplier={editing} onClose={() => setEditing(undefined)} />}
    </Panel>
  );
}
