import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Board, MeResponse, TaskCard } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { personRef } from '@/test/fixtures/adminTasks';
import { demoEventBoard } from '@/test/fixtures/eventBoards';
import { demoCrew, demoMilestones, eventDetailFor } from '@/test/fixtures/eventDetails';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

//----------------------------------------------------------\\
//                              SETUP
//----------------------------------------------------------\\

type Sent = { path: 'edit' | 'assignees' | 'move'; body: Record<string, unknown> };

type Options = {
  me?: MeResponse;
  cardId?: string;
  refuse?: (path: Sent['path']) => Response | undefined;
};

const problem = (status: number, body: object = {}) =>
  HttpResponse.json({ status, ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } });

const event = demoEvents()[1]!; //vantage brand activation
const packKit = 'Pack bar kit and glassware';

//one card from vantage's board, behind its own page, answering writes the way the api does
function openCard({ me = users.eventManager, cardId, refuse }: Options = {}) {
  const board: Board = demoEventBoard(event);
  const all = () => board.columns.flatMap((column) => column.cards);
  const card = all().find((candidate) => candidate.subject === packKit)!;
  const original = { ...card };
  const sent: Sent[] = [];

  function update(changes: Partial<TaskCard>, columnId?: string) {
    const current = all().find((candidate) => candidate.cardId === card.cardId)!;
    const updated = {
      ...current,
      ...changes,
      columnId: columnId ?? current.columnId,
      rowVersion: btoa(`v${sent.length}`),
    };
    board.columns.forEach((column) => {
      column.cards = column.cards.filter((candidate) => candidate.cardId !== card.cardId);
    });
    board.columns.find((column) => column.columnId === updated.columnId)!.cards.push(updated);
    return HttpResponse.json(updated);
  }

  async function record(path: Sent['path'], request: Request) {
    const body = (await request.json()) as Record<string, unknown>;
    sent.push({ path, body });
    return { body, refused: refuse?.(path) };
  }

  const view = renderApp({
    me,
    route: `/events/${event.eventId}/tasks/${cardId ?? card.cardId}`,
    handlers: [
      http.get('/api/events/:eventId', () => HttpResponse.json(eventDetailFor(event))),
      http.get('/api/events/:eventId/milestones', () => HttpResponse.json(demoMilestones(event))),
      http.get('/api/events/:eventId/crew', () => HttpResponse.json(demoCrew(event))),
      http.get('/api/events/:eventId/board', () => HttpResponse.json(board)),
      http.get('/api/cards/:cardId', ({ params }) => {
        const found = all().find((candidate) => candidate.cardId === params.cardId);
        return found ? HttpResponse.json(found) : problem(404);
      }),
      http.patch('/api/cards/:cardId', async ({ request }) => {
        const { body, refused } = await record('edit', request);
        return (
          refused ??
          update({ subject: String(body.subject), priority: body.priority as TaskCard['priority'] })
        );
      }),
      http.put('/api/cards/:cardId/assignees', async ({ request }) => {
        const { body, refused } = await record('assignees', request);
        const people = demoCrew(event).filter((shift) => (body.userIds as string[]).includes(shift.userId));
        return refused ?? update({ assignees: people.map(({ userId, fullName }) => ({ userId, fullName })) });
      }),
      http.post('/api/cards/:cardId/move', async ({ request }) => {
        const { body, refused } = await record('move', request);
        return refused ?? update({}, String(body.columnId));
      }),
    ],
  });

  return { ...view, sent, board, original };
}

const panel = (name: string) => within(screen.getByRole('region', { name }));

//----------------------------------------------------------\\
//                              THE CARD
//----------------------------------------------------------\\

describe('an event task card', () => {
  it('shows what it is, when it is due against its milestone, and who has it', async () => {
    openCard();

    expect(await screen.findByRole('heading', { level: 1, name: packKit })).toBeInTheDocument();
    expect(panel('Card').getByText('Bar kit, two sets')).toBeInTheDocument();
    expect(await panel('Card').findByText('· 4h before load-in')).toBeInTheDocument();
    expect(panel('Card').getByText(/^Load-in · /)).toBeInTheDocument();
    expect(panel('People').getByText('Priya R.')).toBeInTheDocument();
    //sarah added it, and she's the one looking
    expect(panel('People').getByText('You')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Task board' })).toHaveAttribute(
      'href',
      `/events/${event.eventId}/board`,
    );
    expect(await screen.findByText(/· To do$/)).toBeInTheDocument();
  });

  it("isn't found at an event it doesn't belong to", async () => {
    openCard({ cardId: 'ad000000-0000-0000-0000-000000000001' });

    expect(await screen.findByRole('heading', { name: "We can't find that card" })).toBeInTheDocument();
  });

  it('leaves the buttons off for a role that can only read the board', async () => {
    openCard({ me: users.accounts });

    await screen.findByRole('heading', { level: 1, name: packKit });
    expect(screen.queryByRole('button', { name: 'Edit card' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Move to…' })).not.toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = openCard();
    await screen.findByRole('heading', { level: 1, name: packKit });
    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              CHANGES
//----------------------------------------------------------\\

describe('changing a card', () => {
  it('edits it, keeping the due time and milestone exactly as they were', async () => {
    const { sent, original } = openCard();

    await userEvent.click(await screen.findByRole('button', { name: 'Edit card' }));
    const dialog = within(await screen.findByRole('dialog', { name: `Edit ${packKit}` }));
    await dialog.findByRole('option', { name: /^Load-in · / });
    await userEvent.selectOptions(dialog.getByLabelText('Priority'), 'High');
    await userEvent.click(dialog.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({
      path: 'edit',
      body: {
        subject: packKit,
        description: original.description,
        priority: 'High',
        dueAt: original.dueAt,
        milestoneId: original.milestoneId,
        rowVersion: original.rowVersion,
      },
    });
    expect(await screen.findByText(`Saved your changes to ${packKit}.`)).toBeInTheDocument();
  });

  it("assigns people from the event's crew, with the card's version", async () => {
    const { sent, original } = openCard();

    await userEvent.click(await screen.findByRole('button', { name: 'Assign people' }));
    const dialog = within(await screen.findByRole('dialog', { name: `People on ${packKit}` }));
    await userEvent.click(await dialog.findByRole('checkbox', { name: /Thabo N\./ }));
    await userEvent.click(dialog.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      {
        path: 'assignees',
        body: {
          userIds: [personRef('casualCrew').userId, personRef('crewLead').userId],
          rowVersion: original.rowVersion,
        },
      },
    ]);
    expect(await panel('People').findByText('Thabo N. and Priya R.')).toBeInTheDocument();
  });

  it('shows who is on it now when someone else changed the card first, rather than saving over them', async () => {
    const { original } = openCard({
      refuse: (path) =>
        path === 'assignees'
          ? problem(409, {
              type: '/problems/concurrency-conflict',
              current: { ...original, assignees: [personRef('crewLead')], rowVersion: 'dGhlaXJz' },
            })
          : undefined,
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Assign people' }));
    const dialog = within(await screen.findByRole('dialog', { name: `People on ${packKit}` }));
    await userEvent.click(await dialog.findByRole('checkbox', { name: /Priya R\./ }));
    await userEvent.click(dialog.getByRole('button', { name: 'Save' }));

    expect(await dialog.findByText(/^Someone else changed this card just now/)).toBeInTheDocument();
    expect(dialog.getByRole('checkbox', { name: /Thabo N\./ })).toBeChecked();
    expect(dialog.getByRole('checkbox', { name: /Priya R\./ })).not.toBeChecked();
  });

  it('moves it to another column from its own page', async () => {
    const { sent, board } = openCard();

    await userEvent.click(await screen.findByRole('button', { name: 'Move to…' }));
    const dialog = within(await screen.findByRole('dialog', { name: `Move ${packKit}` }));
    await userEvent.click(dialog.getByRole('button', { name: /Move to Doing/ }));

    expect(sent[0]).toMatchObject({ path: 'move', body: { columnId: board.columns[1]!.columnId } });
    expect(await screen.findByText(/· Doing$/)).toBeInTheDocument();
  });
});
