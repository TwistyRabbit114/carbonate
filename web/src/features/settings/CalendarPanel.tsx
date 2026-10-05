import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { isApiError } from '@/api/problem';
import { queryKeys } from '@/api/queryKeys';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { Panel } from '@/components/Panel';
import { useToast } from '@/components/toast/ToastContext';
import { problemMessage } from '@/lib/apiErrors';
import { formatDateTime } from '@/lib/format';
import { useCalendarConnection, useConnectCalendar, useDisconnectCalendar } from './api';
import form from '@/components/FormLayout.module.scss';

const googleConsent = 'https://accounts.google.com/';

//the company google calendar that event dates, milestones and task due dates are pushed to
//(FR-40 to FR-42). carbonate only ever writes to it, and never sends client names or money
export function CalendarPanel() {
  const connection = useCalendarConnection();
  const connect = useConnectCalendar();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  const [confirming, setConfirming] = useState(false);

  //google's consent page is somewhere else, so the browser goes there, but only there. a link
  //back into the app (what the mock answers with) is followed in place, so nothing reloads
  async function startConnecting() {
    try {
      const { authorizationUrl } = await connect.mutateAsync();
      if (authorizationUrl.startsWith('/') && !authorizationUrl.startsWith('//')) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.calendarConnection });
        void navigate(authorizationUrl);
      } else if (authorizationUrl.startsWith(googleConsent)) {
        window.location.assign(authorizationUrl);
      } else {
        toast.error("Google Calendar wasn't connected. The sign-in link didn't go to Google.");
      }
    } catch (error) {
      toast.error(problemMessage(error, "Google Calendar wasn't connected."));
    }
  }

  const notBuilt = connection.isError && isApiError(connection.error) && connection.error.status === 501;

  return (
    <Panel title="Google Calendar">
      {notBuilt ? (
        <Alert>
          Google Calendar sync isn't switched on yet. Event dates still show on Carbonate's own pages.
        </Alert>
      ) : connection.isError ? (
        <ErrorState
          message="We couldn't check the Google Calendar link."
          onRetry={() => void connection.refetch()}
        />
      ) : connection.isPending ? (
        <p role="status">Checking the Google Calendar link…</p>
      ) : (
        <>
          {connection.data.reconnectNeeded && (
            <div className={form.alert}>
              <Alert tone="warning">
                Reconnect needed: Google stopped accepting Carbonate's access, so changes aren't reaching the
                calendar. While Google has the app in testing mode, this happens every 7 days.
              </Alert>
            </div>
          )}
          <FieldList>
            <FieldRow label="Status">
              {connection.data.connected
                ? `Connected to ${connection.data.googleAccountEmail ?? 'a Google account'}`
                : 'Not connected'}
            </FieldRow>
            <FieldRow label="Direction">Outbound only. Carbonate never reads from Google.</FieldRow>
            <FieldRow label="Access">Write calendar events only</FieldRow>
            <FieldRow label="Connected">
              {connection.data.connectedAt && formatDateTime(connection.data.connectedAt)}
            </FieldRow>
            <FieldRow label="Last update sent">
              {connection.data.lastPushedAt && formatDateTime(connection.data.lastPushedAt)}
            </FieldRow>
            <FieldRow label="Last problem">{connection.data.lastError}</FieldRow>
          </FieldList>
          <div className={form.actions}>
            {(!connection.data.connected || connection.data.reconnectNeeded) && (
              <Button variant="primary" busy={connect.isPending} onClick={() => void startConnecting()}>
                {connection.data.connected ? 'Reconnect' : 'Connect Google Calendar'}
              </Button>
            )}
            {connection.data.connected && <Button onClick={() => setConfirming(true)}>Disconnect</Button>}
          </div>
        </>
      )}

      {confirming && <DisconnectDialog onClose={() => setConfirming(false)} />}
    </Panel>
  );
}

function DisconnectDialog({ onClose }: { onClose: () => void }) {
  const disconnect = useDisconnectCalendar();
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);

  async function confirm() {
    setServerError(null);
    try {
      await disconnect.mutateAsync();
      toast.success('Google Calendar is disconnected.');
      onClose();
    } catch (error) {
      setServerError(problemMessage(error, 'It is still connected.'));
    }
  }

  return (
    <Dialog open title="Disconnect Google Calendar?" onClose={onClose}>
      <p className={form.lead}>
        New dates and changes stop going to the calendar. What's already there stays, and nothing in Carbonate
        changes.
      </p>
      {serverError && (
        <div className={form.alert}>
          <Alert tone="danger">{serverError}</Alert>
        </div>
      )}
      <DialogFooter>
        <Button onClick={onClose}>Keep it connected</Button>
        <Button variant="danger" busy={disconnect.isPending} onClick={() => void confirm()}>
          Disconnect
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
