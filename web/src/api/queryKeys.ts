//every query key lives here, so invalidating after a change can't miss one spelt differently.
//everything about one event sits under ['events', id], so invalidating that catches the lot
export const queryKeys = {
  eventsBoard: ['events', 'board'] as const,
  myEvents: ['events', 'mine'] as const,
  event: (eventId: string) => ['events', eventId] as const,
  allowedTransitions: (eventId: string) => ['events', eventId, 'allowed-transitions'] as const,
  milestones: (eventId: string) => ['events', eventId, 'milestones'] as const,
  eventCrew: (eventId: string) => ['events', eventId, 'crew'] as const,
  eventBoard: (eventId: string) => ['events', eventId, 'task-board'] as const,
  siteVisits: (eventId: string) => ['events', eventId, 'site-visits'] as const,
  stockRequirements: (eventId: string) => ['events', eventId, 'stock-requirements'] as const,
  incidents: (eventId: string) => ['events', eventId, 'incidents'] as const,
  quotes: (eventId: string) => ['events', eventId, 'quotes'] as const,
  adminBoard: ['boards', 'admin'] as const,
  card: (cardId: string) => ['cards', cardId] as const,
  activeUsers: ['users', 'active'] as const,
  venue: (venueId: string) => ['venues', venueId] as const,
  venues: ['venues', 'list'] as const,
  clients: ['clients'] as const,
  divisions: ['divisions'] as const,
  stockItems: ['stock', 'items'] as const,
  equipment: ['stock', 'equipment'] as const,
};
