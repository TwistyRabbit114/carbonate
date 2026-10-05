import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const vantageQuote = '9e000000-0000-0000-0000-000000000002';
const meridianQuote = '9e000000-0000-0000-0000-000000000003';

const panel = (name: string) => screen.findByRole('region', { name });
const statusOf = async () => {
  const totals = await panel('Totals');
  return within(totals).getByText('Status').nextElementSibling;
};

//----------------------------------------------------------\\
//                              READING
//----------------------------------------------------------\\

describe('QuotePage reading', () => {
  it('shows the lines, the totals and the margin against its band', async () => {
    const { container } = renderMockedApp('eventManager', `/quotes/${meridianQuote}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Meridian Year-End Function costing' }),
    ).toBeInTheDocument();
    const lines = await panel('Lines');
    expect(within(lines).getByText('Bar staff')).toBeInTheDocument();
    expect(within(lines).getByText('Unit cost to us')).toBeInTheDocument();
    const totals = await panel('Totals');
    expect(within(totals).getByText('R 98 000,00')).toBeInTheDocument();
    expect(within(totals).getByText('R 112 700,00')).toBeInTheDocument();
    expect(within(totals).getByText('R 66 150,00')).toBeInTheDocument();
    expect(within(totals).getByText(/32\.5%/)).toBeInTheDocument();
    //it's over the limit, but it went out after the director approved it
    expect(screen.queryByText(/approval limit/)).not.toBeInTheDocument();
    expect(await axeViolations(container)).toEqual([]);
  });

  //the masked render check for this page: with the cost and margin left out by the api, neither
  //the columns nor the rows are there (NFR-17)
  it('shows nothing about cost or margin when the api leaves them out', async () => {
    renderMockedApp('eventManager', `/quotes/${meridianQuote}`);
    server.use(
      http.get('/api/quotes/:quoteId', () =>
        HttpResponse.json({
          quoteId: meridianQuote,
          eventId: '0e000000-0000-0000-0000-000000000003',
          copiedFromQuoteId: null,
          version: 1,
          status: 'Issued',
          subtotalExVat: 98_000,
          vatAmount: 14_700,
          totalIncVat: 112_700,
          requiresApproval: false,
          validUntil: null,
          issuedAt: '2026-09-01T08:00:00Z',
          acceptedAt: null,
          approvedByUserId: null,
          approvedAt: null,
          createdAt: '2026-08-28T08:00:00Z',
          rowVersion: 'AAAA',
          lines: [
            {
              quoteLineId: 'l1',
              description: 'Bar staff',
              quantity: 1,
              category: 'Crew',
              unitPriceToClient: 24_500,
              lineTotal: 24_500,
            },
          ],
        }),
      ),
    );
    const totals = await panel('Totals');
    await within(totals).findByText('R 112 700,00');

    expect(within(totals).queryByText('Internal cost')).not.toBeInTheDocument();
    expect(within(totals).queryByText('Margin')).not.toBeInTheDocument();
    expect(screen.queryByText('Unit cost to us')).not.toBeInTheDocument();
    expect(screen.queryByText(/visible to Director, Event Manager and Accounts/)).not.toBeInTheDocument();
  });

  it('keeps operations out', async () => {
    renderMockedApp('operationsManager', `/quotes/${meridianQuote}`);

    expect(await screen.findByText(/isn't available for your role/i)).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              APPROVAL AND SENDING
//----------------------------------------------------------\\

describe('QuotePage steps', () => {
  it("tells the event manager it's waiting for the director, with nothing to press", async () => {
    renderMockedApp('eventManager', `/quotes/${vantageQuote}`);

    expect(
      await screen.findByText(/Waiting for the Director\. It's above the approval limit/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Mark as sent to client' })).not.toBeInTheDocument();
  });

  it('lets the director approve it, then it can be sent and accepted', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', `/quotes/${vantageQuote}`);

    await user.click(await screen.findByRole('button', { name: 'Approve' }));
    expect(await screen.findByText('Version 1 is approved.')).toBeInTheDocument();
    await waitFor(async () => expect(await statusOf()).toHaveTextContent('Approved'));

    await user.click(await screen.findByRole('button', { name: 'Mark as sent to client' }));
    await waitFor(async () => expect(await statusOf()).toHaveTextContent('Sent to the client'));

    await user.click(await screen.findByRole('button', { name: 'Client accepted' }));
    await waitFor(async () => expect(await statusOf()).toHaveTextContent('Accepted by the client'));
    expect(screen.queryByRole('button', { name: /Change lines|Make a new version/ })).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              EDITING
//----------------------------------------------------------\\

describe('QuotePage editing', () => {
  it('fills a blank costing, checking each line, and shows the totals the api works out', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/finance');
    const row = (await within(await panel('Costings')).findByText('Atlas Product Launch')).closest('tr')!;
    await user.click(
      await within(row).findByRole('button', { name: 'Start a costing for Atlas Product Launch' }),
    );
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Start costing' }),
    );

    const lines = await panel('Lines');
    await user.click(within(lines).getByRole('button', { name: 'Change lines' }));
    await user.click(within(lines).getByRole('button', { name: 'Save costing' }));
    expect(await within(lines).findByText('Describe the line.')).toBeInTheDocument();
    expect(within(lines).getByLabelText('Line 1 unit cost to us')).toHaveAttribute('aria-invalid', 'true');

    await user.selectOptions(within(lines).getByLabelText('Line 1 category'), 'Crew');
    await user.type(within(lines).getByLabelText('Line 1 description'), 'Bartenders');
    await user.clear(within(lines).getByLabelText('Line 1 quantity'));
    await user.type(within(lines).getByLabelText('Line 1 quantity'), '2');
    await user.type(within(lines).getByLabelText('Line 1 unit cost to us'), '600');
    await user.type(within(lines).getByLabelText('Line 1 unit price to client'), '1 000');
    await user.click(within(lines).getByRole('button', { name: 'Save costing' }));

    expect(await screen.findByText('The costing is saved.')).toBeInTheDocument();
    const totals = await panel('Totals');
    expect(await within(totals).findByText('R 2 000,00')).toBeInTheDocument();
    expect(within(totals).getByText('R 300,00')).toBeInTheDocument();
    expect(within(totals).getByText('R 2 300,00')).toBeInTheDocument();
    expect(within(totals).getByText(/^40%/)).toBeInTheDocument();
    expect(within(totals).getByText('Outside target')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Atlas Product Launch costing' }),
    ).toBeInTheDocument();
    //under the approval limit, so it can go straight to the client
    expect(screen.getByRole('button', { name: 'Mark as sent to client' })).toBeInTheDocument();
  });

  it('makes a new version when a sent costing changes, keeping the old one as it was', async () => {
    const user = userEvent.setup();
    const { router } = renderMockedApp('eventManager', `/quotes/${meridianQuote}`);
    const lines = await panel('Lines');

    await user.click(within(lines).getByRole('button', { name: 'Make a new version' }));
    expect(within(lines).getByText(/saving makes version 2/)).toBeInTheDocument();
    const price = within(lines).getByLabelText('Line 4 unit price to client');
    await user.clear(price);
    await user.type(price, '25 000');
    await user.click(within(lines).getByRole('button', { name: 'Save as version 2' }));

    expect(await screen.findByText(/Version 2 is saved/)).toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).not.toBe(`/quotes/${meridianQuote}`));
    expect(await screen.findByText(/· Version 2/)).toBeInTheDocument();

    await router.navigate(`/quotes/${meridianQuote}`);
    expect(await screen.findByText(/A newer version replaced this one/)).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Open version 2' })).toBeInTheDocument();
  });

  it('offers their version when someone else saved first', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/quotes/${meridianQuote}`);
    server.use(
      http.put('/api/quotes/:quoteId', () =>
        HttpResponse.json(
          {
            status: 409,
            title: 'Someone else changed this costing.',
            type: '/problems/concurrency-conflict',
          },
          { status: 409 },
        ),
      ),
    );
    const lines = await panel('Lines');

    await user.click(within(lines).getByRole('button', { name: 'Make a new version' }));
    await user.click(within(lines).getByRole('button', { name: 'Save as version 2' }));

    expect(
      await within(lines).findByText(/Someone else changed this costing while you were working on it/),
    ).toBeInTheDocument();
    await user.click(within(lines).getByRole('button', { name: 'Load their version' }));
    expect(
      await within(await panel('Lines')).findByRole('button', { name: 'Make a new version' }),
    ).toBeInTheDocument();
  });
});
