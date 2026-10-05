import type { DivisionCode, EventStatus, EventType, MilestoneType } from '@/api/types';

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

export const divisionLabels: Record<DivisionCode, string> = {
  CE: 'Carbon Events',
  CLM: 'Carbon Logistics Management',
};

//a site visit is a recce to the client (Appendix C)
export const milestoneLabels: Record<MilestoneType, string> = {
  SiteVisit: 'Recce',
  LoadIn: 'Load-in',
  Rehearsal: 'Rehearsal',
  Doors: 'Doors',
  Strike: 'Strike',
  LoadOut: 'Load-out',
  Debrief: 'Debrief',
  Invoice: 'Invoice',
  Reconciliation: 'Reconciliation',
};
