import { describe, expect, it } from 'vitest';
import type { TaskCard } from '@/api/types';
import { demoAdminBoard, personRef } from '@/test/fixtures/adminTasks';
import { actionsFor } from './rules';

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
