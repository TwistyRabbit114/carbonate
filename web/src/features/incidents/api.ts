import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { Incident, IncidentType } from '@/api/types';

//incident reports (FR-31), D's api. replacement cost is $cost and only there for roles that see it

export function useEventIncidents(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.incidents(eventId),
    queryFn: () => apiFetch<Incident[]>(`/events/${eventId}/incidents`),
    select: (incidents) => [...incidents].sort((a, b) => b.reportedAt.localeCompare(a.reportedAt)),
    enabled,
  });
}

export type NewIncident = {
  eventId: string;
  incidentType: IncidentType;
  stockItemId: string | null;
  assetId: string | null;
  quantity: number | null;
  description: string;
  photo: File | null;
};

//multipart, so a photo goes with it. the field names are the api's form names
function formOf(incident: NewIncident) {
  const form = new FormData();
  form.set('IncidentType', incident.incidentType);
  if (incident.stockItemId) form.set('StockItemId', incident.stockItemId);
  if (incident.assetId) form.set('AssetId', incident.assetId);
  if (incident.quantity !== null) form.set('Quantity', String(incident.quantity));
  form.set('Description', incident.description);
  if (incident.photo) form.set('Photo', incident.photo);
  return form;
}

//not optimistic: on a weak signal the form keeps everything and offers to send it again
export function useReportIncident() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (incident: NewIncident) =>
      apiFetch<Incident>(`/events/${incident.eventId}/incidents`, { method: 'POST', body: formOf(incident) }),
    onSuccess: (_created, { eventId }) =>
      void queryClient.invalidateQueries({ queryKey: queryKeys.incidents(eventId) }),
  });
}
