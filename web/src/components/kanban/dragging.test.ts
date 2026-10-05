import type { KeyboardCoordinateGetter } from '@dnd-kit/core';
import { describe, expect, it, vi } from 'vitest';
import { columnKeyboardCoordinates } from './dragging';

//three 300px columns side by side, with only the enabled ones handed back as stops
function keyboardContext(overId: string, enabled = ['first', 'second', 'third']) {
  const rect = (left: number) => ({
    left,
    top: 100,
    width: 300,
    height: 500,
    right: left + 300,
    bottom: 600,
  });
  const rects = new Map([
    ['first', rect(0)],
    ['second', rect(320)],
    ['third', rect(640)],
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
    expect(columnKeyboardCoordinates(key('ArrowRight'), keyboardContext('first'))).toEqual({ x: 320, y: 108 });
  });

  it('jumps back to the left', () => {
    expect(columnKeyboardCoordinates(key('ArrowLeft'), keyboardContext('second'))?.x).toBe(0);
  });

  it('skips columns the card is not allowed into', () => {
    const context = keyboardContext('second', ['first', 'second']);
    expect(columnKeyboardCoordinates(key('ArrowRight'), context)).toBeUndefined();
  });

  it('leaves other keys to dnd-kit', () => {
    expect(columnKeyboardCoordinates(key('Space'), keyboardContext('second'))).toBeUndefined();
  });
});
