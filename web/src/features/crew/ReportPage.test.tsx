import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const naidoo = '0e000000-0000-0000-0000-000000000001';
const riverlight = '0e000000-0000-0000-0000-000000000004';

async function fillIn(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('radio', { name: /Breakage/ }));
  const glasses = await screen.findByRole('option', { name: 'Wine glasses' });
  await user.selectOptions(screen.getByLabelText(/Item or equipment/), glasses);
  await user.type(screen.getByLabelText(/How many/), '12');
  await user.type(screen.getByLabelText(/^What happened/), 'Dropped a crate at the back bar.');
}

describe('ReportPage', () => {
  it('picks the live event, and reports a breakage against it', async () => {
    const user = userEvent.setup();
    let sent: FormData | null = null;
    renderMockedApp('crewLead', '/my/report');
    server.use(
      http.post('/api/events/:eventId/incidents', async ({ request, params }) => {
        expect(params.eventId).toBe(riverlight);
        sent = await request.formData();
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    expect(await screen.findByLabelText(/^Event/)).toHaveValue(riverlight);
    await fillIn(user);
    await user.click(screen.getByRole('button', { name: 'Send report' }));

    expect(await screen.findByText('Reported. Thanks, the office can see this now.')).toBeInTheDocument();
    expect(sent!.get('IncidentType')).toBe('Breakage');
    expect(sent!.get('Quantity')).toBe('12');
    expect(sent!.get('StockItemId')).toBeTruthy();
    expect(sent!.get('AssetId')).toBeNull();
  });

  it('starts on the event it was opened from', async () => {
    renderMockedApp('crewLead', `/my/report?event=${naidoo}`);

    expect(await screen.findByLabelText(/^Event/)).toHaveValue(naidoo);
  });

  it('asks for what is missing', async () => {
    const user = userEvent.setup();
    renderMockedApp('crewLead', '/my/report');

    await user.click(await screen.findByRole('button', { name: 'Send report' }));

    expect(await screen.findByText('Choose what kind of problem it was.')).toBeInTheDocument();
    expect(screen.getByText("Choose what it's about.")).toBeInTheDocument();
    expect(screen.getByText('Say what happened.')).toBeInTheDocument();
  });

  it('keeps everything when the send fails, ready to try again', async () => {
    const user = userEvent.setup();
    renderMockedApp('crewLead', '/my/report');
    server.use(http.post('/api/events/:eventId/incidents', () => HttpResponse.error()));

    await fillIn(user);
    await user.click(screen.getByRole('button', { name: 'Send report' }));

    expect(
      await screen.findByText(/It didn't send\. Everything you filled in is still here/),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/^What happened/)).toHaveValue('Dropped a crate at the back bar.');
    expect(screen.getByRole('button', { name: 'Send report' })).toBeEnabled();
  });

  //casual crew can report but the stock lists are closed to them for now, see the TODO(plan)
  it("tells casual crew when they can't pick what broke", async () => {
    renderMockedApp('casualCrew', '/my/report');

    expect(await screen.findByText(/isn't open to your role yet/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send report' })).toBeDisabled();
  });

  it('has no accessibility violations', async () => {
    const { container } = renderMockedApp('crewLead', '/my/report');

    await screen.findByRole('option', { name: 'Wine glasses' });
    expect(await axeViolations(container)).toEqual([]);
  });
});

describe('ReportIncidentPage', () => {
  it('reports from the desk and goes back to the event', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${riverlight}/incidents/new`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Report an incident' })).toBeInTheDocument();
    await fillIn(user);
    await user.click(screen.getByRole('button', { name: 'Send report' }));

    expect(await screen.findByText('Reported. Thanks, the office can see this now.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Done' })).toHaveAttribute('href', `/events/${riverlight}`);
  });
});
