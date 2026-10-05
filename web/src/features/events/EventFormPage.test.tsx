import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const vantage = '0e000000-0000-0000-0000-000000000002';

describe('EventFormPage', () => {
  it('creates an enquiry and opens it', async () => {
    const user = userEvent.setup();
    const { router } = renderMockedApp('eventManager', '/events/new');

    await user.type(await screen.findByLabelText(/Event name/), 'Harbour Launch');
    await user.type(screen.getByLabelText(/Event code/), 'HRB-LNCH-26');
    //the pickers fill in once their lists arrive
    await screen.findByRole('option', { name: 'Vantage Brands' });
    const waterfront = await screen.findByRole('option', { name: 'V&A Waterfront' });
    await screen.findByRole('option', { name: 'Carbon Events' });
    await user.selectOptions(screen.getByLabelText(/Client/), 'Vantage Brands');
    await user.selectOptions(screen.getByLabelText(/Venue/), waterfront);
    await user.selectOptions(screen.getByLabelText(/Division/), 'Carbon Events');
    await user.selectOptions(screen.getByLabelText(/Event type/), 'Activation');
    await user.type(screen.getByLabelText(/Event date/), '2026-12-04');
    await user.type(screen.getByLabelText(/^Starts/), '2026-12-04T10:00');
    await user.type(screen.getByLabelText(/^Ends/), '2026-12-04T18:00');
    await user.type(screen.getByLabelText(/Pack size/), '400');
    await user.type(screen.getByLabelText(/Staff required/), '8');
    await user.type(screen.getByLabelText(/Budget/), '120 000');
    await user.click(screen.getByRole('button', { name: 'Create event' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Harbour Launch' })).toBeInTheDocument();
    expect(router.state.location.pathname).toMatch(/^\/events\/[0-9a-f-]{36}$/);
    expect(screen.getByText(/This is still an enquiry/)).toBeInTheDocument();
    expect(
      await within(await screen.findByRole('region', { name: 'Costing' })).findByText('R 120 000,00'),
    ).toBeInTheDocument();
  });

  it('asks for what is missing before sending anything', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/events/new');

    await user.click(await screen.findByRole('button', { name: 'Create event' }));

    expect(await screen.findByText('Give the event a name.')).toBeInTheDocument();
    expect(screen.getByText('Choose a venue.')).toBeInTheDocument();
    expect(screen.getByText('Choose when the event starts.')).toBeInTheDocument();
  });

  it('shows the api turning down an event code already in use', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${vantage}/edit`);

    const code = await screen.findByLabelText(/Event code/);
    await user.clear(code);
    await user.type(code, 'NAI-WED-26');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('That event code is already in use.')).toBeInTheDocument();
  });

  //the budget is $price: no box for a role that can't see it
  it('leaves the budget off for the operations manager', async () => {
    renderMockedApp('operationsManager', '/events/new');

    await screen.findByLabelText(/Event name/);
    expect(screen.queryByLabelText(/Budget/)).not.toBeInTheDocument();
  });

  it('edits an event and goes back to it', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${vantage}/edit`);

    const name = await screen.findByLabelText(/Event name/);
    expect(name).toHaveValue('Vantage Brand Activation');
    expect(screen.getByLabelText(/Budget/)).toHaveValue('130000');
    await user.clear(name);
    await user.type(name, 'Vantage Summer Activation');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Vantage Summer Activation' }),
    ).toBeInTheDocument();
  });

  it("never quietly saves over someone else's change", async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${vantage}/edit`);

    const name = await screen.findByLabelText(/Event name/);
    //someone else saves the event while this form is open
    let sent: Record<string, unknown> | null = null;
    server.use(
      http.put('/api/events/:eventId', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        if (!sent) {
          sent = body;
          return HttpResponse.json(
            {
              status: 409,
              type: '/problems/concurrency-conflict',
              title: 'Someone else changed this event.',
              current: { ...body, eventId: vantage, name: 'Their name', rowVersion: 'their-version' },
            },
            { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
          );
        }
        sent = body;
        return HttpResponse.json({ ...body, eventId: vantage, status: 'ConfirmedInPlanning' });
      }),
    );
    await user.clear(name);
    await user.type(name, 'My name');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(
      await screen.findByText(/Someone else changed this event while you were editing it/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Keep my changes' }));
    expect(name).toHaveValue('My name');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(sent).toMatchObject({ name: 'My name', rowVersion: 'their-version' }));
  });

  it('loads their version when asked', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${vantage}/edit`);

    const name = await screen.findByLabelText(/Event name/);
    server.use(
      http.put('/api/events/:eventId', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          {
            status: 409,
            type: '/problems/concurrency-conflict',
            current: { ...body, eventId: vantage, name: 'Their name', rowVersion: 'their-version' },
          },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        );
      }),
    );
    await user.clear(name);
    await user.type(name, 'My name');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    await user.click(await screen.findByRole('button', { name: 'Load their version' }));

    expect(name).toHaveValue('Their name');
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeEnabled();
  });

  it('has no accessibility violations', async () => {
    const { container } = renderMockedApp('eventManager', '/events/new');

    await screen.findByRole('option', { name: 'Vantage Brands' });
    expect(await axeViolations(container)).toEqual([]);
  });
});
