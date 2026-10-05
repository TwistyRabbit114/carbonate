import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Board, EventListItem, MeResponse, TaskCard } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { personRef } from '@/test/fixtures/adminTasks';
import { demoEventBoard } from '@/test/fixtures/eventBoards';
import { demoCrew, demoMilestones, eventDetailFor, milestoneOf } from '@/test/fixtures/eventDetails';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

//----------------------------------------------------------\\
//                              SETUP
//----------------------------------------------------------\\

type Options = {
  me?: MeResponse;
  event?: EventListItem;
  board?: Board | null; //null: the event has no task board
  hidden?: boolean; //the event is one this user can't see
  refuse?: () => Response; //answers every write instead of accepting it
  slow?: boolean;
};

const problem = (status: number, body: object = {}) =>
  HttpResponse.json({ status, ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } });

//vantage brand activation, four days out: its recce is done, the rest is still to do
const vantage = () => demoEvents()[1]!;

function openBoard({
  me = users.eventManager,
  event = vantage(),
  board,
  hidden = false,
  refuse,
  slow = false,
}: Options = {}) {
  const live = board === undefined ? demoEventBoard(event) : board;
  const sent: { path: 'move' | 'create'; body: Record<string, unknown> }[] = [];
  const notFound = () => problem(404, { title: 'Not found' });

  const view = renderApp({
    me,
    route: `/events/${event.eventId}/board`,
    handlers: [
      http.get('/api/events/:eventId', () =>
        hidden ? notFound() : HttpResponse.json(eventDetailFor(event)),
      ),
      http.get('/api/events/:eventId/milestones', () =>
        hidden ? notFound() : HttpResponse.json(demoMilestones(event)),
      ),
      http.get('/api/events/:eventId/crew', () => HttpResponse.json(demoCrew(event))),
      http.get('/api/events/:eventId/board', () =>
        hidden || !live ? problem(404, { detail: 'This event has no task board.' }) : HttpResponse.json(live),
      ),
      http.post('/api/cards/:cardId/move', async ({ request, params }) => {
        const body = (await request.json()) as Record<string, unknown>;
        sent.push({ path: 'move', body });
        if (slow) await delay(150);
        if (refuse) return refuse();

        const card = live!.columns
          .flatMap((column) => column.cards)
          .find((other) => other.cardId === params.cardId)!;
        const to = live!.columns.find((column) => column.columnId === body.columnId)!;
        live!.columns.forEach((column) => {
          column.cards = column.cards.filter((other) => other.cardId !== card.cardId);
        });
        const moved: TaskCard = {
          ...card,
          columnId: to.columnId,
          status: to.isDoneColumn ? 'Done' : 'Open',
          rowVersion: 'bmV3LXZlcnNpb24=',
        };
        to.cards.push(moved);
        return HttpResponse.json(moved);
      }),
      http.post('/api/boards/:boardId/cards', async ({ request }) => {
        const body = (await request.json()) as Record<string, unknown>;
        sent.push({ path: 'create', body });
        if (refuse) return refuse();
        const column = live!.columns.find((candidate) => candidate.columnId === body.columnId)!;
        const card: TaskCard = {
          ...column.cards[0]!,
          cardId: 'ca000000-0000-0000-0000-0000000000ff',
          subject: String(body.subject),
          position: column.cards.length,
        };
        column.cards.push(card);
        return HttpResponse.json(card, { status: 201 });
      }),
    ],
  });

  return { ...view, sent, board: live };
}

const column = (name: string) => within(screen.getByRole('region', { name }));

async function openMoveMenu(subject: string) {
  await userEvent.click(await screen.findByRole('button', { name: `Move ${subject} to…` }));
  return within(await screen.findByRole('dialog', { name: `Move ${subject}` }));
}

const packKit = 'Pack bar kit and glassware';

//----------------------------------------------------------\\
//                              THE BOARD
//----------------------------------------------------------\\

describe('an event task board', () => {
  it("shows the template's columns, with the limit on Doing, back to the event it belongs to", async () => {
    openBoard();

    expect(await screen.findByText(packKit)).toBeInTheDocument();
    expect(column('To do').getByText('Confirm ice delivery with Coastal Ice')).toBeInTheDocument();
    expect(column('Doing').getByText('2 / 2')).toBeInTheDocument();
    expect(column('Done').getByText('Recce the venue and loading bay')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Vantage Brand Activation' })).toHaveAttribute(
      'href',
      `/events/${vantage().eventId}`,
    );
  });

  it('says when each card is due, against the milestone the crew know it by', async () => {
    openBoard();

    const card = within((await screen.findByRole('link', { name: packKit })).closest('li')!);
    expect(await card.findByText('4h before load-in')).toBeInTheDocument();
    expect(card.getByText(/^Due /)).toBeInTheDocument();
    expect(card.getByText('Assigned to Priya R.')).toBeInTheDocument();
  });

  it('links each card to its own page', async () => {
    const { board } = openBoard();
    const card = board!.columns[0]!.cards.find((candidate) => candidate.subject === packKit)!;

    expect(await screen.findByRole('link', { name: packKit })).toHaveAttribute(
      'href',
      `/events/${vantage().eventId}/tasks/${card.cardId}`,
    );
  });

  it('carries the confidential banner for a confidential event', async () => {
    openBoard({ event: demoEvents()[0]! });

    expect(await screen.findByText('Confidential. NDA in effect.')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = openBoard();
    await screen.findByText(packKit);
    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              BY ROLE
//----------------------------------------------------------\\

describe('who can do what on the board', () => {
  it('gives crew the cards the api sends them and handles to move them, but no Add card (FR-21)', async () => {
    const board = demoEventBoard(vantage());
    const thabo = personRef('crewLead').userId;
    board.columns.forEach((candidate) => {
      candidate.cards = candidate.cards.filter((card) =>
        card.assignees.some((person) => person.userId === thabo),
      );
    });
    openBoard({ me: users.crewLead, board });

    expect(await screen.findByText('Set up the bar')).toBeInTheDocument();
    expect(screen.queryByText(packKit)).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Drag / }).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Add card' })).not.toBeInTheDocument();
  });

  it('gives accounts a read-only board', async () => {
    openBoard({ me: users.accounts });

    expect(await screen.findByText(packKit)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Drag / })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /to…$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add card' })).not.toBeInTheDocument();
  });

  it('tells crew with nothing on the board yet that their cards will show up here', async () => {
    const board = demoEventBoard(vantage());
    board.columns.forEach((candidate) => {
      candidate.cards = [];
    });
    openBoard({ me: users.casualCrew, board });

    expect(
      await screen.findByRole('heading', { name: 'Nothing on this board for you yet' }),
    ).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              MOVING
//----------------------------------------------------------\\

describe('moving a card', () => {
  it('moves it straight away, then confirms, and shows the column going over its limit', async () => {
    const { sent, board } = openBoard({ slow: true });
    const card = board!.columns[0]!.cards.find((candidate) => candidate.subject === packKit)!;
    const menu = await openMoveMenu(packKit);

    expect(menu.getByText("It's at its limit of 2 already.")).toBeInTheDocument();
    await userEvent.click(menu.getByRole('button', { name: /Move to Doing/ }));

    //optimistic: in its new column before the api has answered (NFR-07)
    expect(await column('Doing').findByText(packKit)).toBeInTheDocument();
    expect(column('Doing').getByText('3 / 2')).toBeInTheDocument();
    expect(sent).toEqual([
      {
        path: 'move',
        body: { columnId: board!.columns[1]!.columnId, position: 2, rowVersion: card.rowVersion },
      },
    ]);
    expect(await screen.findByText(`${packKit} moved to Doing.`)).toBeInTheDocument();
  });

  it('marks a card done when it goes into the done column', async () => {
    openBoard();
    const menu = await openMoveMenu(packKit);

    expect(menu.getByText('Marks it done.')).toBeInTheDocument();
    await userEvent.click(menu.getByRole('button', { name: /Move to Done/ }));

    expect(await screen.findByText(`${packKit} is done.`)).toBeInTheDocument();
    expect(column('Done').getByText(packKit)).toBeInTheDocument();
  });

  it('puts the card back and says why when someone else changed it first', async () => {
    openBoard({ refuse: () => problem(409, { type: '/problems/concurrency-conflict' }) });
    const menu = await openMoveMenu(packKit);

    await userEvent.click(menu.getByRole('button', { name: /Move to Doing/ }));

    expect(
      await screen.findByText(/^Someone else changed Pack bar kit and glassware just now/),
    ).toBeInTheDocument();
    expect(column('To do').getByText(packKit)).toBeInTheDocument();
    expect(column('Doing').getByText('2 / 2')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              ADDING
//----------------------------------------------------------\\

describe('adding a card', () => {
  it('sends the card with its column, due time, milestone and crew', async () => {
    const event = vantage();
    const { sent, board } = openBoard();
    const loadIn = milestoneOf(demoMilestones(event), 'LoadIn');

    await userEvent.click(await screen.findByRole('button', { name: 'Add card' }));
    const dialog = within(await screen.findByRole('dialog', { name: 'Add a card' }));
    await userEvent.type(dialog.getByLabelText(/Subject/), 'Collect the float');
    await userEvent.selectOptions(dialog.getByLabelText(/Column/), 'Doing');
    fireEvent.change(dialog.getByLabelText('Due'), { target: { value: '2026-12-01T08:00' } });
    await userEvent.selectOptions(dialog.getByLabelText('Milestone'), loadIn.milestoneId);
    await userEvent.click(await dialog.findByRole('checkbox', { name: /Thabo N\./ }));
    await userEvent.click(dialog.getByRole('button', { name: 'Add card' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      {
        path: 'create',
        body: {
          subject: 'Collect the float',
          description: null,
          priority: 'Normal',
          dueAt: '2026-12-01T06:00:00.000Z',
          milestoneId: loadIn.milestoneId,
          columnId: board!.columns[1]!.columnId,
          assigneeIds: [personRef('crewLead').userId],
        },
      },
    ]);
    expect(await screen.findByText('Collect the float was added to Doing.')).toBeInTheDocument();
  });

  it("passes on the api's reason when someone isn't on the crew", async () => {
    openBoard({
      refuse: () => problem(422, { detail: "Add them to the event's crew first, then assign the card." }),
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Add card' }));
    const dialog = within(await screen.findByRole('dialog', { name: 'Add a card' }));
    await userEvent.type(dialog.getByLabelText(/Subject/), 'Collect the float');
    await userEvent.click(dialog.getByRole('button', { name: 'Add card' }));

    expect(
      await dialog.findByText("Add them to the event's crew first, then assign the card."),
    ).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              MISSING
//----------------------------------------------------------\\

describe('when there is no board to show', () => {
  it('explains an event that has no task board', async () => {
    openBoard({ board: null });

    expect(await screen.findByRole('heading', { name: 'This event has no task board' })).toBeInTheDocument();
  });

  it("gives the same answer for an event that isn't there and one the user can't see", async () => {
    openBoard({ me: users.casualCrew, hidden: true });

    expect(await screen.findByRole('heading', { name: "We can't find that event" })).toBeInTheDocument();
  });
});
