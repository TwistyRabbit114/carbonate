import type { KeyboardCoordinateGetter } from '@dnd-kit/core';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/problem';
import { demoEvents } from '@/test/fixtures/events';
import { columnKeyboardCoordinates, describeMoveError, moveAnnouncements } from './moves';

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
//                              KEYBOARD
//----------------------------------------------------------\\

//three 300px columns side by side, with only the enabled ones handed back as stops
function keyboardContext(overId: string, enabled = ['ConfirmedInPlanning', 'InProgress', 'Finished']) {
  const rect = (left: number) => ({
    left,
    top: 100,
    width: 300,
    height: 500,
    right: left + 300,
    bottom: 600,
  });
  const rects = new Map([
    ['ConfirmedInPlanning', rect(0)],
    ['InProgress', rect(320)],
    ['Finished', rect(640)],
  ]);
  return {
    context: {
      over: { id: overId },
      collisionRect: rect(0),
      droppableRects: rects,
      droppableContainers: { getEnabled: () => enabled.map((id) => ({ id })) },
    },
  } as unknown as Parameters<KeyboardCoordinateGetter>[1];
}

const key = (code: string) => ({ code, preventDefault: vi.fn() }) as unknown as KeyboardEvent;

describe('columnKeyboardCoordinates', () => {
  it('jumps a whole column to the right', () => {
    expect(columnKeyboardCoordinates(key('ArrowRight'), keyboardContext('ConfirmedInPlanning'))).toEqual({
      x: 320,
      y: 108,
    });
  });

  it('jumps back to the left', () => {
    expect(columnKeyboardCoordinates(key('ArrowLeft'), keyboardContext('InProgress'))?.x).toBe(0);
  });

  it('skips columns the event is not allowed into', () => {
    const context = keyboardContext('InProgress', ['ConfirmedInPlanning', 'InProgress']);
    expect(columnKeyboardCoordinates(key('ArrowRight'), context)).toBeUndefined();
  });

  it('leaves other keys to dnd-kit', () => {
    expect(columnKeyboardCoordinates(key('Space'), keyboardContext('InProgress'))).toBeUndefined();
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
