import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { renderMockedApp } from '@/test/renderMockedApp';

const vantage = '0e000000-0000-0000-0000-000000000002';
const iceList = '0111a000-0000-0000-0000-000000000001';
const barHireList = '0111a000-0000-0000-0000-000000000003';

const panel = (name: string) => screen.findByRole('region', { name });
//the planning panel takes the place of its loading state, so wait for the event picker first
const planningPanel = async () => {
  await screen.findByRole('combobox', { name: 'Event' });
  return panel('Stock requirements');
};
const rowOf = async (container: HTMLElement, text: string) =>
  (await within(container).findByText(text)).closest('tr')!;

//----------------------------------------------------------\\
//                              REQUIREMENTS
//----------------------------------------------------------\\

describe('StockPage requirements', () => {
  it('opens on the event the link names, with its warnings in words', async () => {
    const { container } = renderMockedApp('eventManager', `/stock?event=${vantage}`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Stock & Orders' })).toBeInTheDocument();
    const stock = await planningPanel();
    expect(within(stock).getByLabelText('Event')).toHaveDisplayValue(/Vantage Brand Activation/);
    const ice = await rowOf(stock, 'Ice, bulk');
    expect(within(ice).getByText(/180 kg planned, about 300 kg expected/)).toBeInTheDocument();
    expect(within(ice).getByText(/The supplier needs 5 days' notice/)).toBeInTheDocument();
    expect(within(await rowOf(stock, 'Cups 500 ml')).getByText('OK')).toBeInTheDocument();

    await within(await panel('Order lists')).findByText('Coastal Ice Co.');
    expect(await axeViolations(container)).toEqual([]);
  });

  it('saves a change to the whole list, and the shortfall goes once there is enough', async () => {
    const user = userEvent.setup();
    renderMockedApp('operationsManager', `/stock?event=${vantage}`);
    const stock = await planningPanel();
    await rowOf(stock, 'Ice, bulk');

    await user.click(within(stock).getByRole('button', { name: 'Change quantities' }));
    const ice = within(stock).getByLabelText('Ice, bulk, quantity in kg');
    await user.clear(ice);
    await user.type(ice, '400');
    await user.click(within(stock).getByRole('button', { name: 'Save stock list' }));

    expect(await screen.findByText('The stock list is saved.')).toBeInTheDocument();
    const row = await rowOf(stock, 'Ice, bulk');
    expect(within(row).getByText('400 kg')).toBeInTheDocument();
    expect(within(row).queryByText(/planned, about/)).not.toBeInTheDocument();
    expect(within(row).getByText(/The supplier needs 5 days' notice/)).toBeInTheDocument();
  });

  it('checks each row before saving, and adds and removes lines', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/stock?event=${vantage}`);
    const stock = await planningPanel();
    await rowOf(stock, 'Ice, bulk');

    await user.click(within(stock).getByRole('button', { name: 'Change quantities' }));
    await user.click(within(stock).getByRole('button', { name: 'Take Mobile bar unit off the list' }));
    const cups = within(stock).getByLabelText('Cups 500 ml, quantity in cups');
    await user.clear(cups);
    await user.click(within(stock).getByRole('button', { name: 'Save stock list' }));
    expect(await within(stock).findByText('Enter a quantity of 0 or more.')).toBeInTheDocument();
    expect(cups).toHaveAttribute('aria-invalid', 'true');

    await user.type(cups, '1000');
    await user.click(within(stock).getByRole('button', { name: 'Save stock list' }));

    expect(await screen.findByText('The stock list is saved.')).toBeInTheDocument();
    expect(await within(stock).findByText('1 000 cups')).toBeInTheDocument();
    expect(within(stock).queryByText('Mobile bar unit')).not.toBeInTheDocument();
  });

  it('leaves planning off for accounts, who can read the order lists but not plan', async () => {
    renderMockedApp('accounts', '/stock');

    await within(await panel('Order lists')).findByText('Coastal Ice Co.');
    expect(screen.queryByRole('region', { name: 'Stock requirements' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Generate order lists' })).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              ORDER LISTS
//----------------------------------------------------------\\

describe('StockPage order lists', () => {
  it('generates drafts for the period, one per supplier', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/stock');
    const lists = await panel('Order lists');
    await within(lists).findByText('Coastal Ice Co.');

    await user.click(within(lists).getByRole('button', { name: 'Generate order lists' }));
    const form = await screen.findByRole('dialog', { name: 'Generate order lists' });
    await user.click(within(form).getByRole('button', { name: 'Generate' }));

    const made = await screen.findByRole('dialog', { name: 'Order lists made' });
    expect(within(made).getByRole('link', { name: 'Coastal Ice Co.' })).toBeInTheDocument();
    expect(within(made).getByRole('link', { name: 'Vine & Co. Distributors' })).toBeInTheDocument();
    await user.click(within(made).getByRole('button', { name: 'Done' }));

    await waitFor(() => expect(within(lists).getAllByText('Coastal Ice Co.')).toHaveLength(2));
  });

  it('turns away a period that ends before it starts', async () => {
    const user = userEvent.setup();
    renderMockedApp('operationsManager', '/stock');
    const lists = await panel('Order lists');

    await user.click(within(lists).getByRole('button', { name: 'Generate order lists' }));
    const form = await screen.findByRole('dialog', { name: 'Generate order lists' });
    await user.clear(within(form).getByLabelText(/^Needed until/));
    await user.type(within(form).getByLabelText(/^Needed until/), '2020-01-01');
    await user.click(within(form).getByRole('button', { name: 'Generate' }));

    expect(
      await within(form).findByText('The end of the period cannot be before the start.'),
    ).toBeInTheDocument();
  });

  it('lets accounts approve a list someone else made, then mark it placed', async () => {
    const user = userEvent.setup();
    renderMockedApp('accounts', `/stock/order-lists/${iceList}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Coastal Ice Co. order list' }),
    ).toBeInTheDocument();
    const details = await panel('Details');
    const status = () => within(details).getByText('Status').nextElementSibling;
    expect(status()).toHaveTextContent('Waiting for approval');

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText('The Coastal Ice Co. order list is approved.')).toBeInTheDocument();
    await waitFor(() => expect(status()).toHaveTextContent('Approved'));

    await user.click(await screen.findByRole('button', { name: 'Mark placed' }));
    await waitFor(() => expect(status()).toHaveTextContent('Placed'));
  });

  it("won't offer approval to whoever generated the list", async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/stock');
    const lists = await panel('Order lists');
    await within(lists).findByText('Coastal Ice Co.');

    await user.click(within(lists).getByRole('button', { name: 'Generate order lists' }));
    await user.click(
      within(await screen.findByRole('dialog', { name: 'Generate order lists' })).getByRole('button', {
        name: 'Generate',
      }),
    );
    const made = await screen.findByRole('dialog', { name: 'Order lists made' });
    await user.click(within(made).getByRole('link', { name: 'Vine & Co. Distributors' }));

    await user.click(await screen.findByRole('button', { name: 'Submit for approval' }));
    expect(
      await screen.findByText('You generated this list, so someone else needs to approve it.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('shows the estimated cost to roles that may see it, and no column at all to ops', async () => {
    renderMockedApp('operationsManager', `/stock/order-lists/${barHireList}`);

    const lines = await panel('Lines');
    await within(lines).findByText('Mobile bar unit');
    expect(within(lines).queryByText('Estimated unit cost')).not.toBeInTheDocument();
    expect(lines).not.toHaveTextContent(/R\s?\d/);
  });

  it('shows the estimate to the event manager', async () => {
    renderMockedApp('eventManager', `/stock/order-lists/${barHireList}`);

    const lines = await panel('Lines');
    expect(await within(lines).findByText('Estimated unit cost')).toBeInTheDocument();
    expect(within(lines).getByText('R 950,00')).toBeInTheDocument();
  });

  it('says so when the list is not there', async () => {
    renderMockedApp('eventManager', '/stock/order-lists/0111a000-0000-0000-0000-000000000099');

    expect(await screen.findByText("We can't find that order list")).toBeInTheDocument();
  });
});
