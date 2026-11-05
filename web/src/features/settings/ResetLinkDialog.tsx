import { useState } from 'react';
import type { PasswordResetIssued, UserListItem } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { Field } from '@/components/Field';
import { problemMessage } from '@/lib/apiErrors';
import { formatDateTime } from '@/lib/format';
import { useIssuePasswordReset } from './api';
import form from '@/components/FormLayout.module.scss';

//the token rides after the #, so it never reaches the server's logs or another site as a referrer
export const resetLinkFor = (token: string) =>
  `${window.location.origin}/login/reset#token=${encodeURIComponent(token)}`;

type ResetLinkDialogProps = {
  user: UserListItem;
  onClose: () => void;
};

//someone has lost their password and there's no email sending, so an administrator makes a
//single-use link and hands it over (FR-38). making one closes any earlier link for that person,
//and the link is only shown here, once
export function ResetLinkDialog({ user, onClose }: ResetLinkDialogProps) {
  const issue = useIssuePasswordReset(user.userId);
  const [issued, setIssued] = useState<PasswordResetIssued | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  async function create() {
    setServerError(null);
    try {
      setIssued(await issue.mutateAsync());
    } catch (error) {
      //the one refusal user.manage can meet here is the director rule
      setServerError(
        problemMessage(
          error,
          'No reset link was made.',
          "Only the Director can reset a Director's password.",
        ),
      );
    }
  }

  async function copy(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      setCopied(true);
    } catch {
      //no clipboard access, the box is selectable instead
      setCopied(false);
    }
  }

  return (
    <Dialog open title={`Reset ${user.fullName}'s password`} onClose={onClose}>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}

      {issued ? (
        <>
          <p className={form.lead}>
            Give this link to {user.fullName} yourself, in person or by phone. It works once, until{' '}
            {formatDateTime(issued.expiresAt)}, and signs them out everywhere when they use it.
          </p>
          <Field label="Reset link" full>
            {(field) => (
              <input
                {...field}
                value={resetLinkFor(issued.token)}
                readOnly
                onFocus={(focus) => focus.currentTarget.select()}
              />
            )}
          </Field>
          <p role="status" className={form.hint}>
            {copied ? 'Copied.' : ''}
          </p>
          <DialogFooter>
            <Button onClick={() => void copy(resetLinkFor(issued.token))}>Copy link</Button>
            <Button variant="primary" onClick={onClose}>
              Done
            </Button>
          </DialogFooter>
        </>
      ) : (
        <>
          <p className={form.lead}>
            This makes a link {user.fullName} can use to choose a new password. Any link made for them before
            stops working.
          </p>
          <DialogFooter>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="primary" busy={issue.isPending} onClick={() => void create()}>
              Make reset link
            </Button>
          </DialogFooter>
        </>
      )}
    </Dialog>
  );
}
