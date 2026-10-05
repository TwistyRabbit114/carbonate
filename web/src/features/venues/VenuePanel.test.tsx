import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { renderMockedApp } from '@/test/renderMockedApp';

const naidoo = '0e000000-0000-0000-0000-000000000001';

const venuePanel = () => screen.findByRole('region', { name: 'Venue and recce' });

//recces on the event page (FR-33), added and corrected by anyone with venue.edit
describe('VenuePanel recces', () => {
  it('adds a recce, which shows at the top with the plate as the api stores it', async () => {
    const user = userEvent.setup();
    renderMockedApp('operationsManager', `/events/${naidoo}`);
    const venue = await venuePanel();
    await within(venue).findByText('Sipho M.');

    await user.click(within(venue).getByRole('button', { name: 'Add recce' }));
    const form = await screen.findByRole('dialog', { name: 'Add recce' });
    await user.click(within(form).getByRole('button', { name: 'Add recce' }));
    expect(await within(form).findByText('Enter the date of the visit.')).toBeInTheDocument();

    await user.type(within(form).getByLabelText(/^Visit date/), '2099-01-15');
    await user.type(within(form).getByLabelText('Licence plate'), 'ca 555-123');
    await user.type(within(form).getByLabelText('Driver'), 'Lwazi P.');
    await user.type(within(form).getByLabelText('Notes'), 'Gate code changes monthly, ask security.');
    await user.click(within(form).getByRole('button', { name: 'Add recce' }));

    expect(await screen.findByText('The recce is added.')).toBeInTheDocument();
    const recces = within(venue).getByRole('list', { name: 'Recces' });
    const newest = within(recces).getAllByRole('listitem')[0]!;
    expect(await within(newest).findByText('Lwazi P.')).toBeInTheDocument();
    expect(within(newest).getByText('CA 555-123')).toBeInTheDocument();
    expect(within(newest).getByText('By Ops Manager')).toBeInTheDocument();
  });

  it('turns away a plate with odd characters before sending', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/events/${naidoo}`);
    const venue = await venuePanel();

    await user.click(within(venue).getByRole('button', { name: 'Add recce' }));
    const form = await screen.findByRole('dialog', { name: 'Add recce' });
    await user.type(within(form).getByLabelText(/^Visit date/), '2099-01-15');
    await user.type(within(form).getByLabelText('Licence plate'), 'CA#123');
    await user.click(within(form).getByRole('button', { name: 'Add recce' }));

    expect(
      await within(form).findByText('Use letters, numbers, spaces and dashes only, like CA 123-456.'),
    ).toBeInTheDocument();
  });

  it('corrects a recce that is already there', async () => {
    const user = userEvent.setup();
    const { container } = renderMockedApp('eventManager', `/events/${naidoo}`);
    const venue = await venuePanel();
    await within(venue).findByText('Sipho M.');

    await user.click(within(venue).getByRole('button', { name: /^Edit the recce of/ }));
    const form = await screen.findByRole('dialog', { name: 'Edit recce' });
    expect(await axeViolations(container)).toEqual([]);
    const driver = within(form).getByLabelText('Driver');
    await user.clear(driver);
    await user.type(driver, 'Neo T.');
    await user.click(within(form).getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('The recce notes are updated.')).toBeInTheDocument();
    expect(await within(venue).findByText('Neo T.')).toBeInTheDocument();
    expect(within(venue).queryByText('Sipho M.')).not.toBeInTheDocument();
  });

  it('shows accounts the recce with no way to change it', async () => {
    renderMockedApp('accounts', `/events/${naidoo}`);
    const venue = await venuePanel();

    expect(await within(venue).findByText('Sipho M.')).toBeInTheDocument();
    expect(within(venue).queryByRole('button', { name: 'Add recce' })).not.toBeInTheDocument();
    expect(within(venue).queryByRole('button', { name: /^Edit/ })).not.toBeInTheDocument();
  });
});
