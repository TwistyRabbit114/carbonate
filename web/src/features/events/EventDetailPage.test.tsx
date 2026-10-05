import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { renderMockedApp } from '@/test/renderMockedApp';

const naidoo = '0e000000-0000-0000-0000-000000000001';
const vantage = '0e000000-0000-0000-0000-000000000002';
const riverlight = '0e000000-0000-0000-0000-000000000004';
const atlas = '0e000000-0000-0000-0000-000000000006';

const panel = (name: string) => screen.findByRole('region', { name });

describe('EventDetailPage', () => {
  it('shows the event with its milestones, crew and board', async () => {
    renderMockedApp('eventManager', `/events/${naidoo}`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Naidoo Wedding' })).toBeInTheDocument();
    expect(screen.getByText('Confirmed / In Planning', { exact: false })).toBeInTheDocument();
    expect(screen.getByText(/Confidential\. NDA in effect\./)).toBeInTheDocument();

    const overview = await panel('Overview');
    expect(within(overview).getByText('Naidoo Family')).toBeInTheDocument();
    expect(within(overview).getByText('180 guests (estimated)')).toBeInTheDocument();

    const milestones = await panel('Milestones');
    expect(await within(milestones).findByText('Recce')).toBeInTheDocument();
    expect(within(milestones).getByText('Reconciliation')).toBeInTheDocument();

    const crew = await panel('Crew');
    expect(await within(crew).findByText('Thabo N.')).toBeInTheDocument();

    const board = await panel('Task board');
    expect(await within(board).findByRole('link', { name: 'Open board' })).toHaveAttribute(
      'href',
      `/events/${naidoo}/board`,
    );
  });

  it('shows the service schedule with the peak windows marked', async () => {
    renderMockedApp('eventManager', `/events/${naidoo}`);

    const schedule = await panel('Service schedule');
    expect(within(schedule).getByText('15:30')).toBeInTheDocument();
    expect(within(schedule).getAllByText('Peak')).toHaveLength(2);
  });

  it('gives the event manager the costing, with cost and margin', async () => {
    renderMockedApp('eventManager', `/events/${naidoo}`);

    const costing = await panel('Costing');
    expect(within(costing).getByText('R 90 000,00')).toBeInTheDocument();
    expect(await within(costing).findByText('R 84 500,00')).toBeInTheDocument();
    expect(within(costing).getByText('R 58 200,00')).toBeInTheDocument();
    expect(within(costing).getByText(/31\.1%/)).toBeInTheDocument();
  });

  //the masked render check: the api leaves the money out for ops, so nothing about it shows,
  //not an empty panel or a dash (NFR-17)
  it('shows the operations manager no costing at all', async () => {
    const { container } = renderMockedApp('operationsManager', `/events/${naidoo}`);

    await panel('Overview');
    await within(await panel('Crew')).findByText('Thabo N.');
    expect(screen.queryByRole('region', { name: 'Costing' })).not.toBeInTheDocument();
    expect(container).not.toHaveTextContent('R 90 000');
    expect(container).not.toHaveTextContent('Budget');
  });

  it('warns about stock planned below normal use', async () => {
    renderMockedApp('eventManager', `/events/${vantage}`);

    const stock = await panel('Stock');
    expect(
      await within(stock).findByText(
        /Below normal use for the pack size: 180 kg planned, about 300 kg expected\./,
      ),
    ).toBeInTheDocument();
    expect(within(stock).getByText(/The supplier needs 5 days' notice/)).toBeInTheDocument();
  });

  it('lists incidents with the replacement cost for roles that see costs', async () => {
    renderMockedApp('eventManager', `/events/${riverlight}`);

    const incidents = await panel('Incidents');
    expect(await within(incidents).findByText('Breakage: Wine glasses, 24')).toBeInTheDocument();
    expect(within(incidents).getByText(/Replacement cost R 1 080,00/)).toBeInTheDocument();
  });

  it('says what else moved when a milestone is rescheduled', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${naidoo}`);

    const milestones = await panel('Milestones');
    await user.click(await within(milestones).findByRole('button', { name: 'Reschedule Load-in' }));
    const dialog = await screen.findByRole('dialog', { name: 'Reschedule load-in' });
    const starts = within(dialog).getByLabelText(/Starts/);
    const ends = within(dialog).getByLabelText(/Ends/);
    const later = (value: string) =>
      value.replace(/T(\d\d)/, (_, hour: string) => `T${String(Number(hour) + 1).padStart(2, '0')}`);
    const newStart = later((starts as HTMLInputElement).value);
    const newEnd = later((ends as HTMLInputElement).value);
    await user.clear(starts);
    await user.type(starts, newStart);
    await user.clear(ends);
    await user.type(ends, newEnd);
    await user.click(within(dialog).getByRole('button', { name: 'Reschedule' }));

    const result = await screen.findByRole('dialog', { name: 'Load-in rescheduled' });
    expect(within(result).getByText('Moving load-in moved 7 other milestones with it.')).toBeInTheDocument();
  });

  it('confirms an enquiry once its po is recorded', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${atlas}`);

    expect(await screen.findByText(/This is still an enquiry/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Record PO or deposit' }));
    const dialog = await screen.findByRole('dialog', { name: 'Record PO or deposit' });
    await user.click(within(dialog).getByRole('button', { name: 'Record and confirm' }));
    expect(await within(dialog).findByText("Enter the client's PO number.")).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/PO number/), 'PO-4471');
    await user.type(within(dialog).getByLabelText(/Received on/), '2026-10-05');
    await user.click(within(dialog).getByRole('button', { name: 'Record and confirm' }));

    await waitFor(() => expect(screen.queryByText(/This is still an enquiry/)).not.toBeInTheDocument());
    expect(await screen.findByText('Confirmed / In Planning', { exact: false })).toBeInTheDocument();
  });

  it('sends crew to their own view of the event', async () => {
    const { router } = renderMockedApp('crewLead', `/events/${naidoo}`);

    await waitFor(() => expect(router.state.location.pathname).toBe(`/my/events/${naidoo}`));
  });

  it("says so when the event doesn't exist", async () => {
    renderMockedApp('eventManager', '/events/0e000000-0000-0000-0000-000000000009');

    expect(await screen.findByRole('heading', { name: "We can't find that event" })).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = renderMockedApp('eventManager', `/events/${naidoo}`);

    await within(await panel('Crew')).findByText('Thabo N.');
    await within(await panel('Milestones')).findByText('Recce');
    expect(await axeViolations(container)).toEqual([]);
  });
});
