import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { EventListItem, EventStatus, MeResponse } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

//----------------------------------------------------------\\
//                              SETUP
//----------------------------------------------------------\\

//the lifecycle from plan section 8.3, the real rules live on the server
const allowedFrom: Record<EventStatus, EventStatus[]> = {
  Enquired: [],
  ConfirmedInPlanning: ['InProgress', 'Cancelled'],
  InProgress: ['Finished'],
  Finished: [],
  Cancelled: [],
};

const problem = (body: object) =>
  HttpResponse.json(
    { status: 409, ...body },
    { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
  );

type Options = {
  me?: MeResponse;
  //what the transitions endpoint answers. by default it accepts the move and the board reloads with it
  reject?: () => Response;
  slow?: boolean;
};

function openMovableBoard({ me = users.eventManager, reject, slow = false }: Options = {}) {
  const events: EventListItem[] = demoEvents();
  const sent: unknown[] = [];

  const view = renderApp({
    me,
    route: '/events',
    handlers: [
      http.get('/api/events', () =>
        HttpResponse.json({ items: events, page: 1, pageSize: 200, total: events.length }),
      ),
      http.get('/api/events/:eventId/allowed-transitions', ({ params }) =>
        HttpResponse.json(allowedFrom[events.find((event) => event.eventId === params.eventId)!.status]),
      ),
      http.post('/api/events/:eventId/transitions', async ({ request, params }) => {
        const body = (await request.json()) as { to: EventStatus };
        sent.push(body);
        if (slow) await delay(150);
        if (reject) return reject();

        const event = events.find((candidate) => candidate.eventId === params.eventId)!;
        event.status = body.to;
        event.rowVersion = 'bmV3LXZlcnNpb24=';
        return HttpResponse.json(event);
      }),
    ],
  });

  return { ...view, sent };
}

const column = (name: string) => within(screen.getByRole('region', { name }));

async function openMoveMenu(eventName: string) {
  await userEvent.click(await screen.findByRole('button', { name: `Move ${eventName} to…` }));
  return within(await screen.findByRole('dialog', { name: `Move ${eventName}` }));
}

//----------------------------------------------------------\\
//                              WHO CAN MOVE
//----------------------------------------------------------\\

describe('who can move events', () => {
  it('gives accounts a read-only board with no handles or move menus', async () => {
    openMovableBoard({ me: users.accounts });
    await screen.findByText('Naidoo Wedding');

    expect(screen.queryByRole('button', { name: /^Drag / })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Move .* to…$/ })).not.toBeInTheDocument();
  });

  it('gives the event manager a drag handle with keyboard instructions on every card', async () => {
    openMovableBoard();

    const handle = await screen.findByRole('button', {
      name: 'Drag Vantage Brand Activation to another stage',
    });
    expect(handle).toHaveAttribute('aria-roledescription', 'draggable');
    expect(handle).toHaveAccessibleDescription(/press space to pick it up/);
  });
});

//----------------------------------------------------------\\
//                              MOVE MENU
//----------------------------------------------------------\\

describe('moving with the menu', () => {
  it('moves the card straight away, then confirms once the server agrees', async () => {
    const { sent } = openMovableBoard({ slow: true });
    const menu = await openMoveMenu('Vantage Brand Activation');

    await userEvent.click(menu.getByRole('button', { name: /Move to In Progress/ }));

    //optimistic: in its new column before the server has answered (NFR-07)
    expect(await column('In Progress').findByText('Vantage Brand Activation')).toBeInTheDocument();
    expect(sent).toEqual([{ to: 'InProgress', rowVersion: 'AAAAAAAAB9I=' }]);

    expect(await screen.findByText('Vantage Brand Activation moved to In Progress.')).toBeInTheDocument();
    expect(column('In Progress').getByText('Vantage Brand Activation')).toBeInTheDocument();
  });

  it('explains what each manual move means', async () => {
    openMovableBoard();
    const menu = await openMoveMenu('Vantage Brand Activation');

    expect(
      await menu.findByText('Start it early. It moves here on its own at its start time.'),
    ).toBeInTheDocument();
  });

  it('puts the card back and says why when someone else changed the event first', async () => {
    openMovableBoard({
      reject: () => problem({ type: '/problems/concurrency-conflict', title: 'Changed by someone else' }),
    });
    const menu = await openMoveMenu('Vantage Brand Activation');

    await userEvent.click(menu.getByRole('button', { name: /Move to In Progress/ }));

    expect(
      await screen.findByText(/Someone else changed Vantage Brand Activation just now/),
    ).toBeInTheDocument();
    expect(column('Confirmed / In Planning').getByText('Vantage Brand Activation')).toBeInTheDocument();
    expect(column('In Progress').queryByText('Vantage Brand Activation')).not.toBeInTheDocument();
  });

  it("shows the server's own reason when the lifecycle refuses a move", async () => {
    openMovableBoard({
      reject: () =>
        problem({ type: '/problems/invalid-transition', detail: 'Load-in has not been confirmed yet.' }),
    });
    const menu = await openMoveMenu('Vantage Brand Activation');

    await userEvent.click(menu.getByRole('button', { name: /Move to In Progress/ }));

    expect(await screen.findByText('Load-in has not been confirmed yet.')).toBeInTheDocument();
    expect(column('Confirmed / In Planning').getByText('Vantage Brand Activation')).toBeInTheDocument();
  });

  it('tells you a finished event has nowhere to go', async () => {
    openMovableBoard();
    const menu = await openMoveMenu('Delacroix Corporate Golf Day');

    expect(await menu.findByText('Finished events stay where they are.')).toBeInTheDocument();
    expect(menu.queryByRole('button', { name: /Move to/ })).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              CANCELLING
//----------------------------------------------------------\\

describe('cancelling', () => {
  it('only cancels after a second, explicit confirmation', async () => {
    const { sent } = openMovableBoard();
    const menu = await openMoveMenu('Naidoo Wedding');

    await userEvent.click(await menu.findByRole('button', { name: 'Cancel event…' }));

    const confirm = within(screen.getByRole('dialog', { name: 'Cancel Naidoo Wedding?' }));
    expect(confirm.getByRole('button', { name: 'Keep event' })).toHaveFocus();
    expect(sent).toEqual([]);

    await userEvent.click(confirm.getByRole('button', { name: 'Cancel event' }));

    expect(await screen.findByText('Naidoo Wedding was cancelled.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Naidoo Wedding' })).not.toBeInTheDocument();
    expect(sent).toEqual([{ to: 'Cancelled', rowVersion: 'AAAAAAAAB9E=' }]);

    //the card is gone, so keyboard users land at the start of the page content
    await expect.poll(() => document.activeElement?.id).toBe('main');
  });

  it('goes back to the choices without sending anything when you keep the event', async () => {
    const { sent } = openMovableBoard();
    const menu = await openMoveMenu('Naidoo Wedding');

    await userEvent.click(await menu.findByRole('button', { name: 'Cancel event…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Keep event' }));

    expect(screen.getByRole('dialog', { name: 'Move Naidoo Wedding' })).toBeInTheDocument();
    expect(sent).toEqual([]);
  });

  it('is not offered for a live event', async () => {
    openMovableBoard();
    const menu = await openMoveMenu('Riverlight Festival');

    await menu.findByRole('button', { name: /Move to Finished/ });
    expect(menu.queryByRole('button', { name: 'Cancel event…' })).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              ACCESSIBILITY
//----------------------------------------------------------\\

describe('move accessibility', () => {
  it('keeps focus on the card after it moves, now in its new column', async () => {
    openMovableBoard();
    const menu = await openMoveMenu('Vantage Brand Activation');

    await userEvent.click(await menu.findByRole('button', { name: /Move to In Progress/ }));

    const moved = await column('In Progress').findByRole('button', {
      name: 'Move Vantage Brand Activation to…',
    });
    await expect.poll(() => document.activeElement).toBe(moved);
  });

  it('hands focus back to the menu button when the menu is closed', async () => {
    openMovableBoard();
    const menu = await openMoveMenu('Naidoo Wedding');

    await userEvent.click(menu.getByRole('button', { name: 'Close' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Move Naidoo Wedding to…' })).toHaveFocus();
  });

  it('has no violations with the move menu open', async () => {
    const { container } = openMovableBoard();
    const menu = await openMoveMenu('Vantage Brand Activation');
    await menu.findByRole('button', { name: /Move to In Progress/ });

    expect(await axeViolations(container)).toEqual([]);
  });
});
