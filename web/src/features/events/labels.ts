import type { EventStatus, EventType } from '@/api/types';

//the client's own words for each stage (NFR-05)
export const stageLabels: Record<EventStatus, string> = {
  Enquired: 'Enquired',
  ConfirmedInPlanning: 'Confirmed / In Planning',
  InProgress: 'In Progress',
  Finished: 'Finished',
  Cancelled: 'Cancelled',
};

export const eventTypeLabels: Record<EventType, string> = {
  Activation: 'Activation',
  Corporate: 'Corporate',
  Wedding: 'Wedding',
  Festival: 'Festival',
  YearEnd: 'Year-end',
  Private: 'Private',
};
