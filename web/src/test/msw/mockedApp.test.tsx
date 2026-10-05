import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { renderApp } from '../renderApp';
import { demoMfaCode, demoPassword, handlers, resetMocks } from './handlers';
import { server } from './server';

beforeEach(() => resetMocks());

//the same journeys someone gets from `npm run dev:mocks`, through the real screens
async function logInThroughMocks(email: string) {
  const view = renderApp({ me: null, route: '/login' });
  server.use(...handlers);

  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), demoPassword);
  await user.click(screen.getByRole('button', { name: 'Log in' }));
  return { ...view, user };
}

describe('app against the mock api', () => {
  it('takes the crew lead to their events', async () => {
    await logInThroughMocks('thabo@example.com');

    expect(await screen.findByRole('heading', { level: 1, name: 'My events' })).toBeInTheDocument();
  });

  it('takes the director through the code step to the events board', async () => {
    const { user } = await logInThroughMocks('director@example.com');

    await user.type(await screen.findByLabelText('6-digit code'), demoMfaCode);
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Events Board' })).toBeInTheDocument();
  });
});
