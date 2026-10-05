import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const naidooQuote = '9e000000-0000-0000-0000-000000000001';

const panel = (name: string) => screen.findByRole('region', { name });
const rowOf = async (container: HTMLElement, text: string) =>
  (await within(container).findByText(text)).closest('tr')!;

//----------------------------------------------------------\\
//                              WHO SEES WHAT
//----------------------------------------------------------\\

describe('FinancePage panels', () => {
  it('lands accounts on approvals, costings and invoices, with nothing for axe to flag', async () => {
    const { container } = renderMockedApp('accounts', '/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Quotes & Invoices' })).toBeInTheDocument();
    await within(await panel('Order list approvals')).findByText('Coastal Ice Co.');

    const costings = await panel('Costings');
    const vantage = await rowOf(costings, 'Vantage Brand Activation');
    expect(await within(vantage).findByText('Waiting for the Director')).toBeInTheDocument();
    expect(within(vantage).getByText('Above the approval limit')).toBeInTheDocument();
    expect(within(vantage).getByText('R 126 000,00')).toBeInTheDocument();

    const invoices = await panel('Invoices');
    expect(
      within(await rowOf(invoices, 'INV-2026-0042')).getByRole('button', { name: 'Mark INV-2026-0042 paid' }),
    ).toBeInTheDocument();
    expect(within(await rowOf(invoices, 'INV-2026-0041')).getByText('Paid')).toBeInTheDocument();

    expect(await axeViolations(container)).toEqual([]);
  });

  it('gives the event manager costings and invoices but no approving or marking paid', async () => {
    renderMockedApp('eventManager', '/finance');

    await within(await panel('Costings')).findByText('Naidoo Wedding');
    const invoices = await panel('Invoices');
    await within(invoices).findByText('INV-2026-0042');
    expect(screen.queryByRole('region', { name: 'Order list approvals' })).not.toBeInTheDocument();
    expect(within(invoices).queryByRole('button', { name: /Mark .* paid/ })).not.toBeInTheDocument();
  });

  it('keeps operations out, they hold no finance permissions', async () => {
    renderMockedApp('operationsManager', '/finance');

    expect(await screen.findByText(/isn't available for your role/i)).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              INVOICES
//----------------------------------------------------------\\

describe('FinancePage invoices', () => {
  it('marks an invoice paid, refusing a date before it was sent', async () => {
    const user = userEvent.setup();
    renderMockedApp('accounts', '/finance');
    const invoices = await panel('Invoices');

    await user.click(
      within(await rowOf(invoices, 'INV-2026-0042')).getByRole('button', { name: 'Mark INV-2026-0042 paid' }),
    );
    const form = await screen.findByRole('dialog', { name: 'Mark INV-2026-0042 paid' });
    const paidOn = within(form).getByLabelText(/^Paid on/);
    await user.clear(paidOn);
    await user.type(paidOn, '2020-01-01');
    await user.click(within(form).getByRole('button', { name: 'Mark paid' }));
    expect(await within(form).findByText('It cannot be paid before it was issued.')).toBeInTheDocument();

    await user.clear(paidOn);
    await user.type(paidOn, '2099-01-01');
    await user.click(within(form).getByRole('button', { name: 'Mark paid' }));

    expect(await screen.findByText('INV-2026-0042 is marked paid.')).toBeInTheDocument();
    const row = await rowOf(invoices, 'INV-2026-0042');
    await waitFor(() => expect(within(row).getByText('Paid')).toBeInTheDocument());
    expect(within(row).queryByRole('button')).not.toBeInTheDocument();
  });

  it('narrows the list to one status', async () => {
    const user = userEvent.setup();
    renderMockedApp('accounts', '/finance');
    const invoices = await panel('Invoices');
    await within(invoices).findByText('INV-2026-0042');

    await user.selectOptions(within(invoices).getByLabelText('Show'), 'Paid');

    //the filtered list loads fresh, so wait for it rather than for the old one to go
    await waitFor(() => {
      expect(within(invoices).getByText('INV-2026-0041')).toBeInTheDocument();
      expect(within(invoices).queryByText('INV-2026-0042')).not.toBeInTheDocument();
    });
  });
});

//----------------------------------------------------------\\
//                              APPROVALS
//----------------------------------------------------------\\

describe('FinancePage approvals', () => {
  it('approves a list from the panel, which then leaves it', async () => {
    const user = userEvent.setup();
    renderMockedApp('accounts', '/finance');
    const approvals = await panel('Order list approvals');
    await within(approvals).findByText('Coastal Ice Co.');

    await user.click(within(approvals).getByRole('button', { name: 'Approve the Coastal Ice Co. list' }));

    expect(await screen.findByText('The Coastal Ice Co. order list is approved.')).toBeInTheDocument();
    expect(await within(approvals).findByText('Nothing waiting for approval.')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              STARTING A COSTING
//----------------------------------------------------------\\

describe('FinancePage starting a costing', () => {
  it('starts a blank costing for the enquiry and opens it', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/finance');
    const costings = await panel('Costings');
    const atlas = await rowOf(costings, 'Atlas Product Launch');

    await user.click(
      await within(atlas).findByRole('button', { name: 'Start a costing for Atlas Product Launch' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Start a costing for Atlas Product Launch' });
    await user.click(within(dialog).getByRole('button', { name: 'Start costing' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Atlas Product Launch costing' }),
    ).toBeInTheDocument();
    expect(screen.getByText(/No lines yet/)).toBeInTheDocument();
  });

  it('says when the client has nothing earlier to copy', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/finance');
    const atlas = await rowOf(await panel('Costings'), 'Atlas Product Launch');

    await user.click(
      await within(atlas).findByRole('button', { name: 'Start a costing for Atlas Product Launch' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Start a costing for Atlas Product Launch' });
    await user.click(
      within(dialog).getByRole('radio', { name: 'A copy of an earlier event for this client' }),
    );

    expect(await within(dialog).findByText(/no earlier costed events/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Copy it' })).toBeDisabled();
  });

  //the copy itself is the api's (FR-15). this checks the dialog asks for the right costing
  it("copies the chosen earlier event's current costing", async () => {
    const user = userEvent.setup();
    let sent: unknown;
    renderMockedApp('eventManager', '/finance');
    server.use(
      http.get('/api/events/:eventId/cost-history', () =>
        HttpResponse.json([
          {
            eventId: '0e000000-0000-0000-0000-000000000001',
            eventCode: 'NAI-WED-26',
            name: 'Naidoo Wedding',
            eventDate: '2026-03-01',
            packSize: 180,
            totalIncVat: 97_175,
            internalCostTotal: 58_200,
            marginPercent: 31.1,
          },
        ]),
      ),
      http.post('/api/events/:eventId/quotes/copy', async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json({ status: 422, detail: 'Stopped here on purpose.' }, { status: 422 });
      }),
    );
    const atlas = await rowOf(await panel('Costings'), 'Atlas Product Launch');

    await user.click(
      await within(atlas).findByRole('button', { name: 'Start a costing for Atlas Product Launch' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Start a costing for Atlas Product Launch' });
    await user.click(
      within(dialog).getByRole('radio', { name: 'A copy of an earlier event for this client' }),
    );
    await user.selectOptions(
      await within(dialog).findByLabelText('Copy the costing from'),
      within(dialog).getByRole('option', { name: /Naidoo Wedding/ }),
    );
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Copy it' })).toBeEnabled());
    await user.click(within(dialog).getByRole('button', { name: 'Copy it' }));

    expect(await within(dialog).findByText('Stopped here on purpose.')).toBeInTheDocument();
    expect(sent).toEqual({ sourceQuoteId: naidooQuote });
  });
});
