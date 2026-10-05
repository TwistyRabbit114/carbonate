import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { PagedResult, SiteVisit, SiteVisitRequest, Venue, VenueRequest } from '@/api/types';

//venues persist between events and get reused (FR-32). crew reach one only through an event
//they're on, anyone else gets a 404 (US-23)
export function useVenue(venueId: string | null | undefined) {
  return useQuery({
    queryKey: queryKeys.venue(venueId ?? ''),
    queryFn: () => apiFetch<Venue>(`/venues/${venueId}`),
    enabled: Boolean(venueId),
    staleTime: 5 * 60_000,
  });
}

//every active venue for the picker on the event form, desk roles only. a handful of venues, so
//one list rather than a search
export function useVenues() {
  return useQuery({
    queryKey: queryKeys.venues,
    queryFn: () => apiFetch<PagedResult<Venue>>('/venues?pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name)),
    staleTime: 5 * 60_000,
  });
}

//every venue for settings, the retired ones too so they can be brought back
export function useAllVenues() {
  return useQuery({
    queryKey: queryKeys.allVenues,
    queryFn: () => apiFetch<PagedResult<Venue>>('/venues?includeInactive=true&pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name)),
  });
}

//recces for an event, newest first (FR-33)
export function useSiteVisits(eventId: string) {
  return useQuery({
    queryKey: queryKeys.siteVisits(eventId),
    queryFn: () => apiFetch<SiteVisit[]>(`/events/${eventId}/site-visits`),
    select: (visits) => [...visits].sort((a, b) => b.visitDate.localeCompare(a.visitDate)),
  });
}

//----------------------------------------------------------\\
//                              CHANGES
//----------------------------------------------------------\\

//a new venue, or a change to one, with venue.edit. every list of venues is refreshed after
export function useSaveVenue(venueId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (venue: VenueRequest) =>
      venueId
        ? apiFetch<Venue>(`/venues/${venueId}`, { method: 'PUT', json: venue })
        : apiFetch<Venue>('/venues', { method: 'POST', json: venue }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.venue(saved.venueId), saved);
      void queryClient.invalidateQueries({ queryKey: queryKeys.venues });
      void queryClient.invalidateQueries({ queryKey: queryKeys.allVenues });
    },
  });
}

//a new recce on the event, or a correction to one already there
export function useSaveSiteVisit(eventId: string, siteVisitId: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (visit: SiteVisitRequest) =>
      siteVisitId
        ? apiFetch<SiteVisit>(`/site-visits/${siteVisitId}`, { method: 'PUT', json: visit })
        : apiFetch<SiteVisit>(`/events/${eventId}/site-visits`, { method: 'POST', json: visit }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.siteVisits(eventId) }),
  });
}
