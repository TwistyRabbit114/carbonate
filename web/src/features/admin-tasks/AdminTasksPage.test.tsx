import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Board, CardPriority, MeResponse, TaskCard } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { adminColumnIds, demoAdminBoard, personRef } from '@/test/fixtures/adminTasks';
import { userList, users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';

//----------------------------------------------------------\\
//                              SETUP
//----------------------------------------------------------\\

type Sent = { path: 'create' | 'move' | 'complete' | 'return'; body: Record<string, unknown> };

type Options = {
  me?: MeResponse;
  board?: Board;
  refuse?: () => Response; //answers every write instead of accepting it
};

const problem = (status: number, body: object = {}) =>
  HttpResponse.json({ status, ...body }, { status, headers: { 'Content-Type': 'application/problem+json' } });

//the demo board from plan appendix d, answering writes the way the api does: the card moves to
//the bottom of its new column with a fresh row version, and the board reloads with it
function openAdminBoard({ me = users.operationsManager, board = demoAdminBoard(), refuse }: Options = {}) {
  const sent: Sent[] = [];

  function moveTo(cardId: string, columnId: string, changes: Partial<TaskCard> = {}) {
    const card = board.columns.flatMap((column) => column.cards).find((candidate) => candidate.cardId === cardId)!;
    board.columns.forEach((column) => {
      column.cards = column.cards.filter((candidate) => candidate.cardId !== cardId);
    });
    const moved = { ...card, ...changes, columnId, rowVersion: 'bmV3LXZlcnNpb24=' };
    board.columns.find((column) => column.columnId === columnId)!.cards.push(moved);
    return HttpResponse.json(moved);
  }

  async function record(path: Sent['path'], request: Request) {
    const body = (await request.json()) as Record<string, unknown>;
    sent.push({ path, body });
    return body;
  }

  const view = renderApp({
    me,
    route: '/admin-tasks',
    handlers: [
      http.get('/api/boards/admin', () => HttpResponse.json(board)),
      http.get('/api/users', () => HttpResponse.json({ items: userList(), page: 1, pageSize: 200, total: 6 })),
      http.post('/api/boards/:boardId/cards', async ({ request }) => {
        const body = await record('create', request);
        if (refuse) return refuse();
        const assigned = board.columns[0]!;
        const card: TaskCard = {
          ...assigned.cards[0]!,
          cardId: 'ad000000-0000-0000-0000-0000000000ff',
          subject: String(body.subject),
          description: body.description as string | null,
          priority: body.priority as CardPriority,
          dueAt: body.dueAt as string | null,
          position: assigned.cards.length,
          createdBy: personRef('operationsManager'),
        };
        assigned.cards.push(card);
        return HttpResponse.json(card, { status: 201 });
      }),
      http.post('/api/cards/:cardId/move', async ({ request, params }) => {
        const body = await record('move', request);
        return refuse ? refuse() : moveTo(String(params.cardId), String(body.columnId));
      }),
      http.post('/api/cards/:cardId/complete', async ({ request, params }) => {
        await record('complete', request);
        if (refuse) return refuse();
        return moveTo(String(params.cardId), adminColumnIds.complete, { completedAt: new Date().toISOString() });
      }),
      http.post('/api/cards/:cardId/return', async ({ request, params }) => {
        const body = await record('return', request);
        if (refuse) return refuse();
        return moveTo(String(params.cardId), adminColumnIds.assigned, {
          reviewNotes: String(body.reviewNotes),
          returnedBy: personRef('operationsManager'),
          returnedAt: new Date().toISOString(),
        });
      }),
    ],
  });

  return { ...view, sent };
}

const column = (name: string) => within(screen.getByRole('region', { name }));

async function openMoveMenu(subject: string) {
  await userEvent.click(await screen.findByRole('button', { name: `Move ${subject}…` }));
  return within(await screen.findByRole('dialog', { name: `Move ${subject}` }));
}

const liquorLicence = 'Renew liquor licence for the Riverside venue';
const iceMachine = 'Quarterly ice machine service';

//----------------------------------------------------------\\
//                              THE BOARD
//----------------------------------------------------------\\

describe('the admin tasks board', () => {
  it('shows each column with who has the task and what happens next', async () => {
    openAdminBoard();

    expect(await screen.findByText(iceMachine)).toBeInTheDocument();
    expect(column('Assigned').getByText('Assigned to Thabo N.')).toBeInTheDocument();
    expect(column('Assigned').getByText('High')).toBeInTheDocument();
    expect(column('In Progress / Needs Review').getByText('Waiting for your review')).toBeInTheDocument();
    expect(column('Complete').getByText(/^Signed off /)).toBeInTheDocument();
  });

  it('gives the Assign task button to the director and ops, not the event manager', async () => {
    openAdminBoard({ me: users.eventManager });

    expect(await screen.findByText(iceMachine)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Assign task' })).not.toBeInTheDocument();
  });

  it('puts controls only on cards this user has a step to take', async () => {
    openAdminBoard();
    await screen.findByText(iceMachine);
    //ops handed out the task waiting for review, and has nothing to do with the rest
    expect(screen.getAllByRole('button', { name: /^Move .+…$/ })).toHaveLength(1);
    expect(screen.getByRole('button', { name: `Move ${iceMachine}…` })).toBeInTheDocument();
  });

  it("gives the assignee controls on their own task in Assigned, and none on one they've handed in", async () => {
    openAdminBoard({ me: users.crewLead });

    expect(await screen.findByRole('button', { name: `Move ${liquorLicence}…` })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: `Move ${iceMachine}…` })).not.toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = openAdminBoard();
    await screen.findByText(iceMachine);
    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              HANDING IN AND SIGNING OFF
//----------------------------------------------------------\\

describe('handing in and signing off', () => {
  it('lets the assignee hand a task in from its Move menu', async () => {
    const { sent } = openAdminBoard({ me: users.crewLead });

    const menu = await openMoveMenu(liquorLicence);
    await userEvent.click(menu.getByRole('button', { name: /Hand in for review/ }));

    expect(await column('In Progress / Needs Review').findByText(liquorLicence)).toBeInTheDocument();
    expect(sent).toEqual([
      { path: 'move', body: { columnId: adminColumnIds.review, position: 1, rowVersion: btoa('admin-card-1-v1') } },
    ]);
    expect(await screen.findByText(`${liquorLicence} was handed in to Ops Manager for review.`)).toBeInTheDocument();
  });

  it('lets the manager who handed it out mark it complete', async () => {
    const { sent } = openAdminBoard();

    const menu = await openMoveMenu(iceMachine);
    await userEvent.click(menu.getByRole('button', { name: /Mark complete/ }));

    expect(await column('Complete').findByText(iceMachine)).toBeInTheDocument();
    expect(sent).toEqual([{ path: 'complete', body: { rowVersion: btoa('admin-card-3-v1') } }]);
    expect(await screen.findByText(`${iceMachine} was signed off.`)).toBeInTheDocument();
  });

  it("puts the card back and gives the api's reason when it refuses", async () => {
    const reason = 'Only the manager who handed out this task can complete or return it.';
    openAdminBoard({ refuse: () => problem(403, { title: 'Forbidden', detail: reason }) });

    const menu = await openMoveMenu(iceMachine);
    await userEvent.click(menu.getByRole('button', { name: /Mark complete/ }));

    expect(await screen.findByText(reason)).toBeInTheDocument();
    expect(column('In Progress / Needs Review').getByText(iceMachine)).toBeInTheDocument();
  });

  it('says so when someone else changed the task first', async () => {
    openAdminBoard({ refuse: () => problem(409, { type: '/problems/concurrency-conflict' }) });

    const menu = await openMoveMenu(iceMachine);
    await userEvent.click(menu.getByRole('button', { name: /Mark complete/ }));

    expect(await screen.findByText(/^Someone else changed Quarterly ice machine service just now/)).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              RETURNING
//----------------------------------------------------------\\

describe('returning a task', () => {
  async function openReturnDialog() {
    const menu = await openMoveMenu(iceMachine);
    await userEvent.click(menu.getByRole('button', { name: /Return with notes/ }));
    return within(await screen.findByRole('dialog', { name: `Return ${iceMachine}` }));
  }

  it('needs notes, then sends the task back to Assigned with them', async () => {
    const { sent } = openAdminBoard();
    const dialog = await openReturnDialog();

    await userEvent.click(dialog.getByRole('button', { name: 'Return task' }));
    expect(await dialog.findByText('Say what still needs doing before it comes back.')).toBeInTheDocument();
    expect(sent).toEqual([]);

    await userEvent.type(dialog.getByLabelText(/What still needs doing/), 'Attach the service report.');
    await userEvent.click(dialog.getByRole('button', { name: 'Return task' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      { path: 'return', body: { reviewNotes: 'Attach the service report.', rowVersion: btoa('admin-card-3-v1') } },
    ]);
    expect(await column('Assigned').findByText(iceMachine)).toBeInTheDocument();
    expect(column('Assigned').getByText('Returned')).toBeInTheDocument();
  });

  it('keeps the dialog and the notes open when the api refuses', async () => {
    const reason = "Only a task that's been handed in for review can be completed or returned.";
    openAdminBoard({ refuse: () => problem(409, { type: '/problems/invalid-transition', detail: reason }) });
    const dialog = await openReturnDialog();

    await userEvent.type(dialog.getByLabelText(/What still needs doing/), 'Attach the service report.');
    await userEvent.click(dialog.getByRole('button', { name: 'Return task' }));

    expect(await dialog.findByText(reason)).toBeInTheDocument();
    expect(dialog.getByLabelText(/What still needs doing/)).toHaveValue('Attach the service report.');
  });
});

//----------------------------------------------------------\\
//                              ASSIGNING
//----------------------------------------------------------\\

describe('assigning a task', () => {
  async function openAssignDialog() {
    await userEvent.click(await screen.findByRole('button', { name: 'Assign task' }));
    const dialog = within(await screen.findByRole('dialog', { name: 'Assign a task' }));
    await dialog.findByRole('option', { name: 'Thabo N. · Crew Lead' });
    return dialog;
  }

  async function fillIn(dialog: ReturnType<typeof within>, person = 'Thabo N. · Crew Lead') {
    await userEvent.type(dialog.getByLabelText(/Subject/), 'Book the fire inspection');
    await userEvent.type(dialog.getByLabelText('Description'), 'Call the municipality.');
    await userEvent.selectOptions(dialog.getByLabelText('Priority'), 'High');
    fireEvent.change(dialog.getByLabelText('Due'), { target: { value: '2026-10-20' } });
    await userEvent.selectOptions(dialog.getByLabelText(/Assign to/), dialog.getByRole('option', { name: person }));
  }

  it('checks the form, then sends the task with its due date and who it is for', async () => {
    const { sent } = openAdminBoard();
    const dialog = await openAssignDialog();

    await userEvent.click(dialog.getByRole('button', { name: 'Assign task' }));
    expect(await dialog.findByText('Give the task a subject.')).toBeInTheDocument();
    expect(dialog.getByText('Choose who the task is for.')).toBeInTheDocument();

    await fillIn(dialog);
    await userEvent.click(dialog.getByRole('button', { name: 'Assign task' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(sent).toEqual([
      {
        path: 'create',
        body: {
          subject: 'Book the fire inspection',
          description: '<p>Call the municipality.</p>',
          priority: 'High',
          dueAt: '2026-10-20T17:00:00+02:00',
          assigneeIds: [personRef('crewLead').userId],
        },
      },
    ]);
    expect(await screen.findByText('Book the fire inspection was assigned to Thabo N.')).toBeInTheDocument();
    expect(await column('Assigned').findByText('Book the fire inspection')).toBeInTheDocument();
  });

  it("warns a manager who picks themselves that they won't be able to sign it off", async () => {
    openAdminBoard();
    const dialog = await openAssignDialog();

    await userEvent.selectOptions(
      dialog.getByLabelText(/Assign to/),
      dialog.getByRole('option', { name: 'Ops Manager · Operations Manager' }),
    );

    expect(dialog.getByText("You won't be able to sign off a task you assign to yourself.")).toBeInTheDocument();
  });

  it("puts the api's field errors against the field they're about", async () => {
    openAdminBoard({
      refuse: () => problem(400, { errors: { 'assigneeIds[0]': ["That person isn't an active user."] } }),
    });
    const dialog = await openAssignDialog();

    await fillIn(dialog);
    await userEvent.click(dialog.getByRole('button', { name: 'Assign task' }));

    expect(await dialog.findByText("That person isn't an active user.")).toBeInTheDocument();
    expect(dialog.getByLabelText(/Assign to/)).toHaveAttribute('aria-invalid', 'true');
  });

  it('has no accessibility violations with the dialog open', async () => {
    const { container } = openAdminBoard();
    await openAssignDialog();
    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              EMPTY AND ERROR
//----------------------------------------------------------\\

describe('empty and error states', () => {
  it('says how tasks get here when there are none', async () => {
    const board = demoAdminBoard();
    board.columns.forEach((candidate) => {
      candidate.cards = [];
    });
    openAdminBoard({ board });

    expect(await screen.findByRole('heading', { name: 'No admin tasks yet' })).toBeInTheDocument();
    expect(screen.getByText('Use Assign task to hand one out. It starts here in Assigned.')).toBeInTheDocument();
  });

  it('offers a retry when the board will not load', async () => {
    renderApp({
      me: users.operationsManager,
      route: '/admin-tasks',
      handlers: [http.get('/api/boards/admin', () => problem(500))],
    });

    expect(await screen.findByRole('alert')).toHaveTextContent("We couldn't load the admin tasks.");
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
