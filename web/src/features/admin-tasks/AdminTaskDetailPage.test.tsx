import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Board, MeResponse, TaskCard } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { adminColumnIds, demoAdminBoard, personRef } from '@/test/fixtures/adminTasks';
import { userList, users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

//----------------------------------------------------------\\
//                              SETUP
//----------------------------------------------------------\\

type Sent = { path: 'move' | 'complete' | 'return' | 'edit' | 'assignees'; body: Record<string, unknown> };

type Options = {
  me?: MeResponse;
  board?: Board;
  refuse?: (path: Sent['path']) => Response | undefined; //answers a write instead of accepting it
};

const problem = (status: number, body: object = {}) =>
  HttpResponse.json({ status, ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } });

const liquorLicence = 'ad000000-0000-0000-0000-000000000001'; //thabo's, handed out by ops, in Assigned
const iceMachine = 'ad000000-0000-0000-0000-000000000003'; //thabo's, handed out by ops, waiting for review

//the demo board from plan appendix d behind a task's page, answering writes the way the api does
function openTask(
  cardId: string,
  { me = users.operationsManager, board = demoAdminBoard(), refuse }: Options = {},
) {
  const sent: Sent[] = [];
  const find = (id: string) =>
    board.columns.flatMap((column) => column.cards).find((card) => card.cardId === id);

  function update(id: string, changes: Partial<TaskCard>, columnId?: string) {
    const card = find(id)!;
    const updated = {
      ...card,
      ...changes,
      columnId: columnId ?? card.columnId,
      rowVersion: btoa(`${id}-${sent.length}`),
    };
    board.columns.forEach((column) => {
      column.cards = column.cards.filter((candidate) => candidate.cardId !== id);
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
    route: `/admin-tasks/${cardId}`,
    handlers: [
      http.get('/api/cards/:cardId', ({ params }) => {
        const card = find(String(params.cardId));
        return card ? HttpResponse.json(card) : problem(404, { title: 'Not found' });
      }),
      http.get('/api/boards/admin', () => HttpResponse.json(board)),
      http.get('/api/users', () =>
        HttpResponse.json({ items: userList(), page: 1, pageSize: 200, total: 6 }),
      ),
      http.post('/api/cards/:cardId/move', async ({ request, params }) => {
        const { body, refused } = await record('move', request);
        return (
          refused ??
          update(String(params.cardId), { status: 'InProgressOrNeedsReview' }, String(body.columnId))
        );
      }),
      http.post('/api/cards/:cardId/complete', async ({ request, params }) => {
        const { refused } = await record('complete', request);
        return (
          refused ??
          update(
            String(params.cardId),
            { status: 'Complete', completedAt: new Date().toISOString() },
            adminColumnIds.complete,
          )
        );
      }),
      http.post('/api/cards/:cardId/return', async ({ request, params }) => {
        const { body, refused } = await record('return', request);
        return (
          refused ??
          update(
            String(params.cardId),
            {
              status: 'Assigned',
              reviewNotes: String(body.reviewNotes),
              returnedBy: personRef('operationsManager'),
              returnedAt: new Date().toISOString(),
            },
            adminColumnIds.assigned,
          )
        );
      }),
      http.patch('/api/cards/:cardId', async ({ request, params }) => {
        const { body, refused } = await record('edit', request);
        return refused ?? update(String(params.cardId), { subject: String(body.subject) });
      }),
      http.put('/api/cards/:cardId/assignees', async ({ request, params }) => {
        const { body, refused } = await record('assignees', request);
        const ids = body.userIds as string[];
        const people = userList().filter((user) => ids.includes(user.userId));
        return (
          refused ??
          update(String(params.cardId), {
            assignees: people.map(({ userId, fullName }) => ({ userId, fullName })),
          })
        );
      }),
    ],
  });

  return { ...view, sent, board };
}

const panel = (name: string) => within(screen.getByRole('region', { name }));

//waits for the page to load the panel first
const findPanel = async (name: string) => within(await screen.findByRole('region', { name }));

//----------------------------------------------------------\\
//                              THE TASK
//----------------------------------------------------------\\

describe('an admin task', () => {
  it('shows what the task is, who has it and who handed it out', async () => {
    openTask(liquorLicence, { me: users.crewLead });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Renew liquor licence for the Riverside venue' }),
    ).toBeInTheDocument();
    expect(panel('Task').getByText(/municipal portal/)).toBeInTheDocument();
    expect(panel('Task').getByText('High')).toBeInTheDocument();
    expect(panel('Assignment').getByText('you')).toBeInTheDocument();
    expect(panel('Assignment').getByText('Ops Manager')).toBeInTheDocument();
    expect(panel('Assignment').getByText('None, a general task')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Admin Tasks' })).toHaveAttribute('href', '/admin-tasks');
  });

  it('shows a stored description without running anything in it', async () => {
    const board = demoAdminBoard();
    board.columns[0]!.cards[0]!.description = '<p>Check this<img src=x onerror="alert(1)"></p>';
    openTask(liquorLicence, { board });

    expect(await screen.findByText('Check this')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Task' }).querySelector('img')).toBeNull();
  });

  it("puts the manager's notes at the top when a task has come back", async () => {
    const board = demoAdminBoard();
    Object.assign(board.columns[0]!.cards[0]!, {
      reviewNotes: 'Attach the proof of submission.',
      returnedBy: personRef('operationsManager'),
      returnedAt: '2026-10-01T10:00:00Z',
    });
    openTask(liquorLicence, { me: users.crewLead, board });

    expect(await screen.findByText('Attach the proof of submission.')).toBeInTheDocument();
    expect(screen.getByText('Returned by Ops Manager on 01 Oct 2026')).toBeInTheDocument();
  });

  it("says it can't find a task the api won't show, the same as one that doesn't exist", async () => {
    openTask('ad000000-0000-0000-0000-0000000000ee');

    expect(await screen.findByRole('heading', { name: "We can't find that task" })).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = openTask(iceMachine);
    await screen.findByRole('region', { name: 'Manager review' });
    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              WHO SEES WHAT
//----------------------------------------------------------\\

describe('the next step on a task (FR-20)', () => {
  it('gives the assignee a way to hand it in, and no review panel', async () => {
    const { sent } = openTask(liquorLicence, { me: users.crewLead });

    await userEvent.click(await screen.findByRole('button', { name: 'Hand in for review' }));

    expect(sent).toEqual([
      {
        path: 'move',
        body: { columnId: adminColumnIds.review, position: 1, rowVersion: btoa('admin-card-1-v1') },
      },
    ]);
    expect(await screen.findByText(/was handed in to Ops Manager for review\.$/)).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Hand in for review' })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole('region', { name: 'Manager review' })).not.toBeInTheDocument();
  });

  it('shows the review panel only to the manager who handed the task out', async () => {
    openTask(iceMachine, { me: users.director });
    expect(await screen.findByRole('region', { name: 'Assignment' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Manager review' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Mark complete' })).not.toBeInTheDocument();
  });

  it('tells that manager they are waiting while the task is still with the assignee', async () => {
    openTask(liquorLicence);

    expect(
      (await findPanel('Manager review')).getByText('Waiting for Thabo N. to hand it in.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hand in for review' })).not.toBeInTheDocument();
  });

  it('lets that manager sign a handed-in task off', async () => {
    const { sent } = openTask(iceMachine);
    const review = await findPanel('Manager review');

    await userEvent.click(review.getByRole('button', { name: 'Mark complete' }));

    expect(sent).toEqual([{ path: 'complete', body: { rowVersion: btoa('admin-card-3-v1') } }]);
    expect(await review.findByText(/^You signed this off on /)).toBeInTheDocument();
  });

  it('lets that manager send it back with notes, which then show at the top', async () => {
    const { sent } = openTask(iceMachine);

    await userEvent.click(
      (await findPanel('Manager review')).getByRole('button', { name: 'Return with notes…' }),
    );
    const dialog = within(
      await screen.findByRole('dialog', { name: 'Return Quarterly ice machine service' }),
    );
    await userEvent.type(dialog.getByLabelText(/What still needs doing/), 'Attach the service report.');
    await userEvent.click(dialog.getByRole('button', { name: 'Return task' }));

    expect(sent).toEqual([
      {
        path: 'return',
        body: { reviewNotes: 'Attach the service report.', rowVersion: btoa('admin-card-3-v1') },
      },
    ]);
    expect(await screen.findByText(/^Returned by Ops Manager on /)).toBeInTheDocument();
    expect(screen.getByText('Attach the service report.')).toBeInTheDocument();
  });

  it("gives the api's reason when it turns a step down", async () => {
    const reason = 'Only the manager who handed out this task can complete or return it.';
    openTask(iceMachine, { refuse: () => problem(403, { detail: reason }) });

    await userEvent.click((await findPanel('Manager review')).getByRole('button', { name: 'Mark complete' }));

    expect(await screen.findByText(reason)).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              EDITING AND REASSIGNING
//----------------------------------------------------------\\

describe('editing and reassigning', () => {
  it('sends every field, keeping what was not touched exactly as the api sent it', async () => {
    const board = demoAdminBoard();
    const original = { ...board.columns[0]!.cards[0]! };
    const { sent } = openTask(liquorLicence, { board });

    await userEvent.click(await screen.findByRole('button', { name: 'Edit task' }));
    const dialog = within(await screen.findByRole('dialog', { name: `Edit ${original.subject}` }));
    await userEvent.clear(dialog.getByLabelText(/Subject/));
    await userEvent.type(dialog.getByLabelText(/Subject/), 'Renew the Riverside liquor licence');
    await userEvent.click(dialog.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      {
        path: 'edit',
        body: {
          subject: 'Renew the Riverside liquor licence',
          description: original.description,
          priority: 'High',
          dueAt: original.dueAt,
          milestoneId: null,
          rowVersion: original.rowVersion,
        },
      },
    ]);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Renew the Riverside liquor licence' }),
    ).toBeInTheDocument();
  });

  it('never saves over a change someone else made, unless the user chooses to keep theirs', async () => {
    const theirs = {
      ...demoAdminBoard().columns[0]!.cards[0]!,
      subject: 'Their subject',
      rowVersion: 'dGhlaXJz',
    };
    let refusals = 1;
    const { sent } = openTask(liquorLicence, {
      refuse: (path) =>
        path === 'edit' && refusals-- > 0
          ? problem(409, { type: '/problems/concurrency-conflict', current: theirs })
          : undefined,
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Edit task' }));
    const dialog = within(await screen.findByRole('dialog'));
    await userEvent.type(dialog.getByLabelText(/Subject/), ' now');
    await userEvent.click(dialog.getByRole('button', { name: 'Save changes' }));

    expect(
      await dialog.findByText(/Someone else changed this card while you were editing it/),
    ).toBeInTheDocument();
    expect(dialog.getByRole('button', { name: 'Save changes' })).toBeDisabled();

    await userEvent.click(dialog.getByRole('button', { name: 'Keep my changes' }));
    await userEvent.click(dialog.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(sent).toHaveLength(2));
    expect(sent[1]!.body).toMatchObject({
      subject: 'Renew liquor licence for the Riverside venue now',
      rowVersion: 'dGhlaXJz',
    });
  });

  it("reassigns with the card's version, and keeps someone on it", async () => {
    const { sent } = openTask(liquorLicence);

    await userEvent.click(await screen.findByRole('button', { name: 'Reassign' }));
    const dialog = within(await screen.findByRole('dialog', { name: /^People on / }));
    await userEvent.click(await dialog.findByRole('checkbox', { name: /Thabo N\./ }));
    await userEvent.click(dialog.getByRole('button', { name: 'Save' }));
    expect(await dialog.findByText('Choose who should do this task.')).toBeInTheDocument();
    expect(sent).toEqual([]);

    await userEvent.click(dialog.getByRole('checkbox', { name: /Priya R\./ }));
    await userEvent.click(dialog.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      {
        path: 'assignees',
        body: { userIds: [personRef('casualCrew').userId], rowVersion: btoa('admin-card-1-v1') },
      },
    ]);
    expect(await panel('Assignment').findByText('Priya R.')).toBeInTheDocument();
  });

  it('leaves reassigning to the director and ops', async () => {
    openTask(iceMachine, { me: users.accounts });

    await screen.findByRole('region', { name: 'Assignment' });
    expect(screen.queryByRole('button', { name: 'Reassign' })).not.toBeInTheDocument();
  });
});
