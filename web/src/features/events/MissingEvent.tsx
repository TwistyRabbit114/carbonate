import { SearchX } from 'lucide-react';
import { BackLink } from '@/components/BackLink';
import { EmptyState } from '@/components/EmptyState';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';

type MissingEventProps = {
  backTo?: string;
  backLabel?: string;
};

//a 404 from the api, which is also what an event that isn't shared with you looks like.
//its own file so the crew pages don't pull in the desk event page with it
export function MissingEvent({ backTo = '/events', backLabel = 'Events' }: MissingEventProps) {
  return (
    <>
      <PageHead title="Event not found" eyebrow={<BackLink to={backTo}>{backLabel}</BackLink>} />
      <EmptyState title="We can't find that event" icon={SearchX}>
        <p>It may have been removed, or it isn't shared with you.</p>
        <LinkButton to={backTo}>Back to {backLabel.toLowerCase()}</LinkButton>
      </EmptyState>
    </>
  );
}
