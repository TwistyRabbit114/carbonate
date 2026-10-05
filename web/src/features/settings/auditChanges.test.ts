import { describe, expect, it } from 'vitest';
import { actionLabel, auditChanges, fieldLabel } from './auditChanges';

describe('audit changes', () => {
  it('lists only the fields that changed, in words', () => {
    const changes = auditChanges({
      beforeJson: JSON.stringify({ packSizeEstimated: 160, staffRequired: 10, name: 'Naidoo Wedding' }),
      afterJson: JSON.stringify({ packSizeEstimated: 180, staffRequired: 10, name: 'Naidoo Wedding' }),
    });

    expect(changes).toEqual([{ field: 'Pack size estimated', before: '160', after: '180' }]);
  });

  it('shows what a new record was created with, with no before', () => {
    const changes = auditChanges({
      beforeJson: null,
      afterJson: JSON.stringify({ name: 'Old Mill', isActive: true }),
    });

    expect(changes).toEqual([
      { field: 'Name', before: null, after: 'Old Mill' },
      { field: 'Is active', before: null, after: 'yes' },
    ]);
  });

  it('reads lists and empty values plainly', () => {
    const changes = auditChanges({
      beforeJson: JSON.stringify({ roles: ['CrewLead'], notes: null }),
      afterJson: JSON.stringify({ roles: ['CrewLead', 'CasualCrew'], notes: 'Moved shift' }),
    });

    expect(changes).toEqual([
      { field: 'Roles', before: 'CrewLead', after: 'CrewLead, CasualCrew' },
      { field: 'Notes', before: 'nothing', after: 'Moved shift' },
    ]);
  });

  it('keeps text that is not json as it is, rather than failing', () => {
    expect(auditChanges({ beforeJson: '<b>not json', afterJson: null })).toEqual([
      { field: 'Value', before: '<b>not json', after: null },
    ]);
  });

  it('turns codes into labels', () => {
    expect(fieldLabel('requiresHealthSafetyFile')).toBe('Requires health safety file');
    expect(actionLabel('quote.new_version')).toBe('Quote new version');
  });
});
