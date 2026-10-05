import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { renderMockedApp } from '@/test/renderMockedApp';

const panel = (name: string) => screen.findByRole('region', { name });
const rowOf = async (container: HTMLElement, text: string) =>
  (await within(container).findByText(text)).closest('tr')!;

describe('CataloguePage', () => {
  it('gives the director the standard cost column and the cost box', async () => {
    const user = userEvent.setup();
    const { container } = renderMockedApp('director', '/stock/catalogue');
    const items = await panel('Stock items');
    const ice = await rowOf(items, 'Ice, bulk');

    expect(within(items).getByText('Standard cost')).toBeInTheDocument();
    expect(within(ice).getByText('R 4,20')).toBeInTheDocument();
    expect(within(ice).getByText('Coastal Ice Co.')).toBeInTheDocument();
    expect(await axeViolations(container)).toEqual([]);

    await user.click(within(items).getByRole('button', { name: 'Edit Ice, bulk' }));
    const form = await screen.findByRole('dialog', { name: 'Edit Ice, bulk' });
    expect(within(form).getByLabelText(/^Standard cost/)).toHaveValue('4,2');
  });

  //ops manage the catalogue but can't see costs, so neither the column nor the box is there,
  //and saving leaves the stored cost alone (NFR-17)
  it('gives ops no cost anywhere, and an edit keeps the cost they cannot see', async () => {
    const user = userEvent.setup();
    const { container } = renderMockedApp('operationsManager', '/stock/catalogue');
    const items = await panel('Stock items');
    await rowOf(items, 'Ice, bulk');
    expect(within(items).queryByText('Standard cost')).not.toBeInTheDocument();
    expect(container).not.toHaveTextContent(/R\s?\d/);

    await user.click(within(items).getByRole('button', { name: 'Edit Ice, bulk' }));
    const form = await screen.findByRole('dialog', { name: 'Edit Ice, bulk' });
    expect(within(form).queryByLabelText(/^Standard cost/)).not.toBeInTheDocument();
    const perHundred = within(form).getByLabelText(/^Used per 100 guests/);
    await user.clear(perHundred);
    await user.type(perHundred, '60');
    await user.click(within(form).getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Ice, bulk is updated.')).toBeInTheDocument();
    expect(within(await rowOf(items, 'Ice, bulk')).getByText('60')).toBeInTheDocument();
  });

  it('adds a supplier and puts a refusal from the api on its field', async () => {
    const user = userEvent.setup();
    renderMockedApp('operationsManager', '/stock/catalogue');
    const suppliers = await panel('Suppliers');
    await within(suppliers).findByText('Coastal Ice Co.');

    await user.click(within(suppliers).getByRole('button', { name: 'Add supplier' }));
    const form = await screen.findByRole('dialog', { name: 'Add supplier' });
    await user.type(within(form).getByLabelText(/^Name/), 'Atlantic Glass Hire');
    await user.click(within(form).getByRole('button', { name: 'Add supplier' }));
    expect(await within(form).findByText('Enter the lead time in whole days.')).toBeInTheDocument();

    await user.type(within(form).getByLabelText(/^Lead time/), '4');
    await user.click(within(form).getByRole('button', { name: 'Add supplier' }));

    expect(await screen.findByText('Atlantic Glass Hire is added.')).toBeInTheDocument();
    expect(within(await rowOf(suppliers, 'Atlantic Glass Hire')).getByText('4 days')).toBeInTheDocument();
  });

  it('records a new ice machine, and refuses a serial number already in use', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/stock/catalogue');
    const equipment = await panel('Equipment');
    await within(equipment).findByText('IM-0042');

    await user.click(within(equipment).getByRole('button', { name: 'Add equipment' }));
    const form = await screen.findByRole('dialog', { name: 'Add equipment' });
    await user.selectOptions(within(form).getByLabelText(/^Kind/), 'Ice machine');
    await user.type(within(form).getByLabelText(/^Serial number/), 'IM-0042');
    await user.click(within(form).getByRole('button', { name: 'Add equipment' }));
    expect(await within(form).findByText('That serial number is already recorded.')).toBeInTheDocument();

    await user.clear(within(form).getByLabelText(/^Serial number/));
    await user.type(within(form).getByLabelText(/^Serial number/), 'IM-0063');
    await user.click(within(form).getByRole('button', { name: 'Add equipment' }));

    expect(await screen.findByText('IM-0063 is recorded.')).toBeInTheDocument();
    expect(await within(equipment).findByText('IM-0063')).toBeInTheDocument();
  });

  it('is read-only for a crew lead', async () => {
    renderMockedApp('crewLead', '/stock/catalogue');
    const items = await panel('Stock items');
    await rowOf(items, 'Ice, bulk');

    expect(screen.queryByRole('button', { name: /^(Add|Edit)/ })).not.toBeInTheDocument();
  });
});
