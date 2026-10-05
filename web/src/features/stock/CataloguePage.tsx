import { BackLink } from '@/components/BackLink';
import { PageHead } from '@/components/PageHead';
import { EquipmentPanel } from './EquipmentPanel';
import { ItemsPanel } from './ItemsPanel';
import { SuppliersPanel } from './SuppliersPanel';

//the stock catalogue, its suppliers and serialised equipment (FR-24). anyone with stock.view
//reads it, stock.manage changes it
export default function CataloguePage() {
  return (
    <>
      <PageHead title="Catalogue" eyebrow={<BackLink to="/stock">Stock & Orders</BackLink>} />
      <ItemsPanel />
      <SuppliersPanel />
      <EquipmentPanel />
    </>
  );
}
