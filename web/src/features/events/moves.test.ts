import { describe, expect, it } from 'vitest';
import { ApiError } from '@/api/problem';
import { demoEvents } from '@/test/fixtures/events';
import { describeMoveError, moveAnnouncements } from './moves';

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

describe('describeMoveError', () => {
  const name = 'Vantage Brand Activation';

  it('explains a concurrency conflict in plain words', () => {
    const error = new ApiError({ status: 409, type: '/problems/concurrency-conflict' });
    expect(describeMoveError(error, name)).toMatch(/^Someone else changed Vantage Brand Activation/);
  });

  it("passes on the server's reason for a refused transition", () => {
    const error = new ApiError({ status: 409, type: '/problems/invalid-transition', detail: 'Not yet.' });
    expect(describeMoveError(error, name)).toBe('Not yet.');
  });

  it('covers no permission, a vanished event and no connection', () => {
    expect(describeMoveError(new ApiError({ status: 403 }), name)).toBe(
      "Your role can't move events between stages.",
    );
    expect(describeMoveError(new ApiError({ status: 404 }), name)).toBe(
      "Vantage Brand Activation isn't on the board any more.",
    );
    expect(describeMoveError(ApiError.network(), name)).toMatch(/^Couldn't reach Carbonate/);
  });
});

//----------------------------------------------------------\\
//                              ANNOUNCEMENTS
//----------------------------------------------------------\\

describe('moveAnnouncements', () => {
  const events = demoEvents();
  const announce = moveAnnouncements((id) => events.find((event) => event.eventId === id));
  const vantage = { id: '0e000000-0000-0000-0000-000000000002' };

  it('names the event and stages instead of ids', () => {
    expect(announce.onDragStart({ active: vantage } as never)).toBe(
      'Picked up Vantage Brand Activation, currently in Confirmed / In Planning.',
    );
    expect(announce.onDragOver({ active: vantage, over: { id: 'InProgress' } } as never)).toBe(
      'Vantage Brand Activation is over In Progress.',
    );
    expect(announce.onDragEnd({ active: vantage, over: { id: 'InProgress' } } as never)).toBe(
      'Moving Vantage Brand Activation to In Progress.',
    );
  });

  it('says the event stays put when dropped back where it started', () => {
    expect(announce.onDragEnd({ active: vantage, over: { id: 'ConfirmedInPlanning' } } as never)).toBe(
      'Vantage Brand Activation stays in Confirmed / In Planning.',
    );
  });
});
