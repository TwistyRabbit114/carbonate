import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { EventListItem, MeResponse } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

const page = (items: EventListItem[]) => ({ items, page: 1, pageSize: 200, total: items.length });

//opens the board with the given events coming back from GET /api/events
function openBoard(events: EventListItem[] = demoEvents(), me: MeResponse = users.eventManager) {
  const requests: URL[] = [];
  const view = renderApp({
    me,
    route: '/events',
    handlers: [
      http.get('/api/events', ({ request }) => {
        requests.push(new URL(request.url));
        return HttpResponse.json(page(events));
      }),
    ],
  });
  return { ...view, requests };
}

const column = (name: string) => screen.getByRole('region', { name });

//a card is the list item around its title link
const card = (scope: ReturnType<typeof within>, name: string) =>
  within(scope.getByRole('link', { name }).closest('li')!);

//----------------------------------------------------------\\
//                              COLUMNS
//----------------------------------------------------------\\

describe('events board', () => {
  it('shows the three active stages with their counts, and marks the automatic ones', async () => {
    const { requests } = openBoard();
    await screen.findByText('Naidoo Wedding');

    expect(within(column('Confirmed / In Planning')).getByText('3')).toBeInTheDocument();
    expect(within(column('In Progress')).getByText('1')).toBeInTheDocument();
    expect(within(column('Finished')).getByText('1')).toBeInTheDocument();

    expect(within(column('In Progress')).getByText('Auto')).toBeInTheDocument();
    expect(within(column('Finished')).getByText('Auto')).toBeInTheDocument();
    expect(within(column('Confirmed / In Planning')).queryByText('Auto')).not.toBeInTheDocument();

    expect(requests[0]?.searchParams.get('view')).toBe('board');
  });

  it('puts each event in its stage, with the details from the prototype', async () => {
    openBoard();
    await screen.findByText('Naidoo Wedding');

    const naidoo = card(within(column('Confirmed / In Planning')), 'Naidoo Wedding');
    expect(naidoo.getByText('Confidential')).toBeInTheDocument();
    expect(naidoo.getByText('180 guests · Wedding')).toBeInTheDocument();

    const live = card(within(column('In Progress')), 'Riverlight Festival');
    expect(live.getByText('Live now · Riverside Grounds')).toBeInTheDocument();
    expect(live.getByText(/^2\s500 guests · Festival$/)).toBeInTheDocument();

    //the actual pack size wins once it's known
    expect(within(column('Finished')).getByText('86 guests · Corporate')).toBeInTheDocument();
  });

  it('lists upcoming events soonest first', async () => {
    openBoard();
    await screen.findByText('Naidoo Wedding');

    const titles = within(column('Confirmed / In Planning'))
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(titles).toEqual(['Vantage Brand Activation', 'Naidoo Wedding', 'Meridian Year-End Function']);
  });

  it('never shows enquired or cancelled events, even if the api sends them', async () => {
    const [naidoo] = demoEvents();
    const cancelled = {
      ...naidoo!,
      eventId: 'cancelled',
      name: 'Cancelled Gala',
      status: 'Cancelled' as const,
    };
    openBoard([...demoEvents(), cancelled]);
    await screen.findByText('Naidoo Wedding');

    expect(screen.queryByText('Atlas Product Launch')).not.toBeInTheDocument();
    expect(screen.queryByText('Cancelled Gala')).not.toBeInTheDocument();
  });

  it('links each card to its event', async () => {
    openBoard();

    expect(await screen.findByRole('link', { name: 'Naidoo Wedding' })).toHaveAttribute(
      'href',
      '/events/0e000000-0000-0000-0000-000000000001',
    );
  });
});

//----------------------------------------------------------\\
//                              ACTIONS BY ROLE
//----------------------------------------------------------\\

describe('new event button', () => {
  it('is there for roles that create events', async () => {
    openBoard();

    expect(await screen.findByRole('link', { name: 'New event' })).toHaveAttribute('href', '/events/new');
  });

  it('is left out for accounts, who only read the board', async () => {
    openBoard(demoEvents(), users.accounts);
    await screen.findByText('Naidoo Wedding');

    expect(screen.queryByRole('link', { name: 'New event' })).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              STATES
//----------------------------------------------------------\\

describe('board states', () => {
  it('shows a board-shaped placeholder while loading', async () => {
    renderApp({
      me: users.eventManager,
      route: '/events',
      handlers: [http.get('/api/events', () => delay('infinite'))],
    });

    expect(await screen.findByText('Loading the board…')).toBeInTheDocument();
  });

  it('says what went wrong and loads again on retry', async () => {
    let calls = 0;
    renderApp({
      me: users.eventManager,
      route: '/events',
      handlers: [
        //fails twice, since a server error gets one automatic retry before the page gives up
        http.get('/api/events', () => {
          calls++;
          return calls <= 2 ? new HttpResponse(null, { status: 500 }) : HttpResponse.json(page(demoEvents()));
        }),
      ],
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Naidoo Wedding')).toBeInTheDocument();
    expect(screen.queryByText("We couldn't load the events board.")).not.toBeInTheDocument();
  });

  it('explains an empty board instead of showing three empty columns', async () => {
    openBoard([]);

    expect(await screen.findByRole('heading', { name: 'No confirmed events yet' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'In Progress' })).not.toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = openBoard();
    await screen.findByText('Naidoo Wedding');

    expect(await axeViolations(container)).toEqual([]);
  });
});
