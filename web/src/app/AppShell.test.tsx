import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { axeViolations } from '@/test/axe';
import { users } from '@/test/fixtures/users';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderApp';

const mainNav = () => screen.getByRole('navigation', { name: 'Main' });

//----------------------------------------------------------\\
//                              LANDING AND LAYOUT
//----------------------------------------------------------\\

describe('app shell', () => {
  it('lands the director on the events board in the desk layout', async () => {
    const { router } = renderApp({ me: users.director });

    expect(await screen.findByRole('heading', { level: 1, name: 'Events Board' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/events');
    expect(within(mainNav()).getByRole('link', { name: 'Events' })).toHaveAttribute('aria-current', 'page');
  });

  it('shows who is signed in and in which role', async () => {
    renderApp({ me: users.eventManager });

    const banner = await screen.findByRole('banner');
    expect(within(banner).getByText('Sarah M.')).toBeInTheDocument();
    expect(within(banner).getByText('Event Manager')).toBeInTheDocument();
  });

  it('lands accounts on quotes and invoices', async () => {
    const { router } = renderApp({ me: users.accounts });

    expect(await screen.findByRole('heading', { level: 1, name: 'Quotes & Invoices' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/finance');
  });

  it('gives casual crew the tab bar and their events first', async () => {
    renderApp({ me: users.casualCrew });

    expect(await screen.findByRole('heading', { level: 1, name: 'My events' })).toBeInTheDocument();
    const tabs = within(mainNav())
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(tabs).toEqual(['My events', 'My tasks', 'Calendar', 'Report']);
  });

  it('turns ops away from finance without showing the page', async () => {
    renderApp({ me: users.operationsManager, route: '/finance' });

    expect(
      await screen.findByRole('heading', { name: "This isn't available for your role" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Quotes & Invoices' })).not.toBeInTheDocument();
  });

  it('shows the not found page for an unknown address', async () => {
    renderApp({ me: users.director, route: '/nowhere' });

    expect(await screen.findByRole('heading', { name: "We couldn't find that" })).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              SESSION
//----------------------------------------------------------\\

describe('session', () => {
  it('sends someone without a session to login, remembering where they were going', async () => {
    const { router } = renderApp({ me: null, route: '/stock' });

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(router.state.location.search).toBe('?next=%2Fstock');
  });

  it('logs out, clears the session and starts the next person fresh', async () => {
    const logout = vi.fn(() => new HttpResponse(null, { status: 204 }));
    const { router } = renderApp({ me: users.eventManager });
    server.use(http.post('/api/auth/logout', logout));

    await userEvent.click(await screen.findByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(router.state.location.search).toBe('');
    expect(logout).toHaveBeenCalledOnce();
  });
});

//----------------------------------------------------------\\
//                              ACCESSIBILITY
//----------------------------------------------------------\\

describe('accessibility', () => {
  it('has no violations in the desk layout', async () => {
    const { container } = renderApp({ me: users.director });
    await screen.findByRole('heading', { level: 1, name: 'Events Board' });

    expect(await axeViolations(container)).toEqual([]);
  });

  it('has no violations in the crew layout', async () => {
    const { container } = renderApp({ me: users.crewLead });
    await screen.findByRole('heading', { level: 1, name: 'My events' });

    expect(await axeViolations(container)).toEqual([]);
  });
});
