import { describe, expect, it } from 'vitest';
import { ApiError } from '@/api/problem';
import type { TaskCard } from '@/api/types';
import { demoAdminBoard, personRef } from '@/test/fixtures/adminTasks';
import { actionsFor, describeTaskError } from './rules';

//----------------------------------------------------------\\
//                              WHO CAN DO WHAT
//----------------------------------------------------------\\

//handed out by ops, assigned to thabo
const card: TaskCard = demoAdminBoard().columns[0]!.cards[0]!;
const ops = personRef('operationsManager').userId;
const thabo = personRef('crewLead').userId;
const director = personRef('director').userId;

describe('actionsFor', () => {
  it('lets the assignee hand a task in from Assigned, and nobody else', () => {
    expect(actionsFor(card, 'assigned', thabo, false)).toEqual(['handIn']);
    expect(actionsFor(card, 'assigned', ops, true)).toEqual([]);
  });

  it('lets the manager who handed it out complete or return it once it is in review', () => {
    expect(actionsFor(card, 'review', ops, true)).toEqual(['complete', 'return']);
    expect(actionsFor(card, 'assigned', ops, true)).toEqual([]);
  });

  it('keeps sign-off from a different manager and from anyone without admin_task.review', () => {
    expect(actionsFor(card, 'review', director, true)).toEqual([]);
    expect(actionsFor(card, 'review', ops, false)).toEqual([]);
  });

  //the same rule as the api: an assignee never signs off their own task, whatever their role
  it('never offers sign-off to an assignee, even the manager who handed it to themselves', () => {
    const selfAssigned = { ...card, assignees: [personRef('operationsManager')] };
    expect(actionsFor(selfAssigned, 'review', ops, true)).toEqual([]);
  });

  it('offers nothing once a task is complete, or on a column it does not recognise', () => {
    expect(actionsFor(card, 'complete', ops, true)).toEqual([]);
    expect(actionsFor(card, undefined, thabo, true)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

describe('describeTaskError', () => {
  const subject = 'Update PPE stock list';

  it('explains a concurrency conflict in plain words', () => {
    const error = new ApiError({ status: 409, type: '/problems/concurrency-conflict' });
    expect(describeTaskError(error, subject)).toMatch(/^Someone else changed Update PPE stock list/);
  });

  it("passes on the api's reason for a refusal", () => {
    const notYours = 'Only the manager who handed out this task can complete or return it.';
    expect(describeTaskError(new ApiError({ status: 403, detail: notYours }), subject)).toBe(notYours);
    expect(describeTaskError(new ApiError({ status: 409, detail: 'Not yet.' }), subject)).toBe('Not yet.');
  });

  it('covers a refusal with no reason, a vanished card and no connection', () => {
    expect(describeTaskError(new ApiError({ status: 403 }), subject)).toBe(
      "Your role can't do that on Update PPE stock list.",
    );
    expect(describeTaskError(new ApiError({ status: 404 }), subject)).toBe(
      "Update PPE stock list isn't on the board any more.",
    );
    expect(describeTaskError(ApiError.network(), subject)).toMatch(/^Couldn't reach Carbonate/);
  });
});
