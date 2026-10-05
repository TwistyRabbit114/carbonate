import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { renderMockedApp } from '@/test/renderMockedApp';

const group = (name: string) => screen.findByRole('region', { name: new RegExp(`^${name}`) });

describe('MyTasksPage', () => {
  it("groups the crew member's cards by event, then their general tasks", async () => {
    renderMockedApp('casualCrew', '/my/tasks');

    const naidoo = await group('Naidoo Wedding');
    expect(
      await within(naidoo).findByRole('link', { name: 'Pack bar kit and glassware' }),
    ).toBeInTheDocument();
    //thabo's cards never reach priya
    expect(
      within(naidoo).queryByRole('link', { name: 'Recce the venue and loading bay' }),
    ).not.toBeInTheDocument();

    const general = await group('General tasks');
    expect(await within(general).findByRole('link', { name: 'Update PPE stock list' })).toBeInTheDocument();
  });

  it('starts a card and marks it done with buttons rather than dragging', async () => {
    const user = userEvent.setup();
    renderMockedApp('casualCrew', '/my/tasks');

    const naidoo = await group('Naidoo Wedding');
    await user.click(await within(naidoo).findByRole('button', { name: 'Start Pack bar kit and glassware' }));
    expect(await screen.findByText('Pack bar kit and glassware moved to Doing.')).toBeInTheDocument();

    await user.click(
      await within(naidoo).findByRole('button', { name: 'Mark Pack bar kit and glassware done' }),
    );
    expect(await screen.findByText('Pack bar kit and glassware is done.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(naidoo).queryByRole('button', { name: /Pack bar kit and glassware/ }),
      ).not.toBeInTheDocument(),
    );
  });

  it('hands a general task in for review', async () => {
    const user = userEvent.setup();
    renderMockedApp('casualCrew', '/my/tasks');

    const general = await group('General tasks');
    await user.click(
      await within(general).findByRole('button', { name: 'Submit Update PPE stock list for review' }),
    );

    expect(await within(general).findByText(/for review$/)).toBeInTheDocument();
    expect(within(general).queryByRole('button', { name: /Update PPE stock list/ })).not.toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = renderMockedApp('crewLead', '/my/tasks');

    await within(await group('Naidoo Wedding')).findByRole('link', { name: 'Set up the bar' });
    await within(await group('General tasks')).findAllByRole('link');
    expect(await axeViolations(container)).toEqual([]);
  });
});
