import type { IncidentType } from '@/api/types';

//the client's common failures in plain words (FR-31)
export const incidentTypeLabels: Record<IncidentType, string> = {
  Breakage: 'Breakage',
  EquipmentFailure: 'Equipment failure',
  StockShortfall: 'Stock shortfall',
};
