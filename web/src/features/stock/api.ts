import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { EquipmentAsset, PagedResult, StockItem, StockRequirement } from '@/api/types';

//D's endpoints, read here for the event page and the incident form

//an event's planned stock with its shortfall and lead-time warnings (FR-26, FR-27, FR-29)
export function useStockRequirements(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.stockRequirements(eventId),
    queryFn: () => apiFetch<StockRequirement[]>(`/events/${eventId}/stock-requirements`),
    enabled,
  });
}

//what an incident can be about: an item from the catalogue, or a serialised asset. the catalogue
//is a few dozen lines, so one list rather than a search
export function useStockItems() {
  return useQuery({
    queryKey: queryKeys.stockItems,
    queryFn: () => apiFetch<PagedResult<StockItem>>('/stock/items?isActive=true&pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name)),
    staleTime: 5 * 60_000,
  });
}

export function useEquipment() {
  return useQuery({
    queryKey: queryKeys.equipment,
    queryFn: () => apiFetch<EquipmentAsset[]>('/equipment'),
    staleTime: 5 * 60_000,
  });
}
