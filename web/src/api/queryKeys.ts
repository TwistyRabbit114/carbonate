//every query key lives here, so invalidating after a change can't miss one spelt differently
export const queryKeys = {
  eventsBoard: ['events', 'board'] as const,
  event: (eventId: string) => ['events', eventId] as const,
  allowedTransitions: (eventId: string) => ['events', eventId, 'allowed-transitions'] as const,
  milestones: (eventId: string) => ['events', eventId, 'milestones'] as const,
  eventCrew: (eventId: string) => ['events', eventId, 'crew'] as const,
  eventBoard: (eventId: string) => ['events', eventId, 'task-board'] as const,
  adminBoard: ['boards', 'admin'] as const,
  card: (cardId: string) => ['cards', cardId] as const,
  activeUsers: ['users', 'active'] as const,
};
