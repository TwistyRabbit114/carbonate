import { Construction } from 'lucide-react';
import { EmptyState } from '@/components/EmptyState';
import { PageHead } from '@/components/PageHead';

type ComingSoonProps = {
  title: string;
  eyebrow?: string;
};

//stands in for a screen until it's built, so routes and navigation can be checked end to end
export function ComingSoon({ title, eyebrow }: ComingSoonProps) {
  return (
    <>
      <PageHead title={title} eyebrow={eyebrow} />
      <EmptyState title="This screen is still being built" icon={Construction}>
        <p>Everything around it is in place, the screen itself is coming next.</p>
      </EmptyState>
    </>
  );
}
