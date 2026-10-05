import { usePermissions } from '@/auth/AuthContext';
import { PageHead } from '@/components/PageHead';
import { ApprovalsPanel } from './ApprovalsPanel';
import { CostingsPanel } from './CostingsPanel';
import { InvoicesPanel } from './InvoicesPanel';

//the bookkeeping screen: costings, invoices and order approvals, each for the permission it
//takes. operations has none of them, so never gets here (plan section 15, item 2)
//TODO(plan): "delivered, not yet invoiced" (FR-16) waits on C's uninvoiced-events endpoint, which
//still answers 501
export default function FinancePage() {
  const check = usePermissions();

  return (
    <>
      <PageHead title="Quotes & Invoices" eyebrow="Bookkeeping" />
      {check.can('order.approve') && <ApprovalsPanel />}
      {check.can('quote.view') && <CostingsPanel />}
      {check.can('invoice.view') && <InvoicesPanel />}
    </>
  );
}
