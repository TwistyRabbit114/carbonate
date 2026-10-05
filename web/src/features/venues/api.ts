import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { PagedResult, SiteVisit, Venue } from '@/api/types';

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

//recces for an event, newest first (FR-33)
export function useSiteVisits(eventId: string) {
  return useQuery({
    queryKey: queryKeys.siteVisits(eventId),
    queryFn: () => apiFetch<SiteVisit[]>(`/events/${eventId}/site-visits`),
    select: (visits) => [...visits].sort((a, b) => b.visitDate.localeCompare(a.visitDate)),
  });
}
