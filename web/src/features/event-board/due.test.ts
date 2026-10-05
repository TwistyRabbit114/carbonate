import { describe, expect, it } from 'vitest';
import type { Milestone, MilestoneType } from '@/api/types';
import { dueAgainstMilestone } from './due';

const milestone = (milestoneType: MilestoneType, scheduledStart: string): Milestone => ({
  milestoneId: 'm1',
  eventId: 'e1',
  milestoneType,
  scheduledStart,
  scheduledEnd: scheduledStart,
  status: 'Planned',
  predecessorIds: [],
});

const loadIn = milestone('LoadIn', '2026-12-02T08:00:00Z');

describe('dueAgainstMilestone', () => {
  it('says how long before or after the milestone a card is due, the way crew say it', () => {
    expect(dueAgainstMilestone('2026-12-02T06:00:00Z', loadIn)).toBe('2h before load-in');
    expect(dueAgainstMilestone('2026-12-02T09:30:00Z', loadIn)).toBe('1h 30m after load-in');
    expect(dueAgainstMilestone('2026-12-02T07:15:00Z', loadIn)).toBe('45m before load-in');
  });

  it('counts in days once it is two days or more, and says "at" when it lines up', () => {
    expect(dueAgainstMilestone('2026-11-29T08:00:00Z', loadIn)).toBe('3 days before load-in');
    expect(dueAgainstMilestone('2026-12-02T08:00:00Z', loadIn)).toBe('At load-in');
  });

  it('calls a site visit a recce', () => {
    expect(dueAgainstMilestone('2026-11-20T09:00:00Z', milestone('SiteVisit', '2026-11-20T08:00:00Z'))).toBe(
      '1h after recce',
    );
  });
});
