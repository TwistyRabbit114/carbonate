//every query key lives here, so invalidating after a change can't miss one spelt differently
export const queryKeys = {
  eventsBoard: ['events', 'board'] as const,
  allowedTransitions: (eventId: string) => ['events', eventId, 'allowed-transitions'] as const,
};
