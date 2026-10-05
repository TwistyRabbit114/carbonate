import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type {
  EquipmentAsset,
  GenerateOrderListsResponse,
  OrderList,
  OrderListStatus,
  PagedResult,
  SaveEquipmentAssetRequest,
  SaveStockItemRequest,
  SaveStockRequirement,
  SaveSupplierRequest,
  StockCategory,
  StockItem,
  StockRequirement,
  Supplier,
} from '@/api/types';

//D's stock endpoints (FR-24 to FR-30)

//----------------------------------------------------------\\
//                              REQUIREMENTS
//----------------------------------------------------------\\

//an event's planned stock with its shortfall and lead-time warnings (FR-26, FR-27, FR-29)
export function useStockRequirements(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.stockRequirements(eventId),
    queryFn: () => apiFetch<StockRequirement[]>(`/events/${eventId}/stock-requirements`),
    enabled: enabled && Boolean(eventId),
  });
}

//the whole list goes back at once. the answer has the warnings worked out again
export function useSaveStockRequirements(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (items: SaveStockRequirement[]) =>
      apiFetch<StockRequirement[]>(`/events/${eventId}/stock-requirements`, {
        method: 'PUT',
        json: { items },
      }),
    onSuccess: (saved) => queryClient.setQueryData(queryKeys.stockRequirements(eventId), saved),
  });
}

//----------------------------------------------------------\\
//                              CATALOGUE
//----------------------------------------------------------\\

//what an incident can be about, or what an event can plan for. the catalogue is a few dozen lines,
//so one list rather than a search
export function useStockItems() {
  return useQuery({
    queryKey: queryKeys.stockItems,
    queryFn: () => apiFetch<PagedResult<StockItem>>('/stock/items?isActive=true&pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name)),
    staleTime: 5 * 60_000,
  });
}

//the catalogue screen's list, retired items included. standardUnitCost is only on it for roles
//that can see costs
export function useCatalogueItems() {
  return useQuery({
    queryKey: [...queryKeys.stockItems, 'all'],
    queryFn: () => apiFetch<PagedResult<StockItem>>('/stock/items?pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name)),
  });
}

export function useStockCategories() {
  return useQuery({
    queryKey: queryKeys.stockCategories,
    queryFn: () => apiFetch<StockCategory[]>('/stock/categories'),
    select: (categories) => [...categories].sort((a, b) => a.name.localeCompare(b.name)),
    staleTime: 5 * 60_000,
  });
}

export function useSuppliers() {
  return useQuery({
    queryKey: queryKeys.suppliers,
    queryFn: () => apiFetch<Supplier[]>('/suppliers'),
    select: (suppliers) => [...suppliers].sort((a, b) => a.name.localeCompare(b.name)),
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

//a new record, or a change to one, with stock.manage. each refreshes its own list
function useCatalogueSave<TRequest, TSaved>(path: string, key: readonly unknown[], id: string | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (record: TRequest) =>
      id
        ? apiFetch<TSaved>(`${path}/${id}`, { method: 'PUT', json: record })
        : apiFetch<TSaved>(path, { method: 'POST', json: record }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: key }),
  });
}

export const useSaveStockItem = (stockItemId: string | null) =>
  useCatalogueSave<SaveStockItemRequest, StockItem>('/stock/items', queryKeys.stockItems, stockItemId);

export const useSaveSupplier = (supplierId: string | null) =>
  useCatalogueSave<SaveSupplierRequest, Supplier>('/suppliers', queryKeys.suppliers, supplierId);

export const useSaveEquipment = (assetId: string | null) =>
  useCatalogueSave<SaveEquipmentAssetRequest, EquipmentAsset>('/equipment', queryKeys.equipment, assetId);

//----------------------------------------------------------\\
//                              ORDER LISTS
//----------------------------------------------------------\\

//newest first. a status narrows it, accounts' approvals use PendingApproval
export function useOrderLists(status?: OrderListStatus) {
  return useQuery({
    queryKey: [...queryKeys.orderLists, status ?? 'all'],
    queryFn: () =>
      apiFetch<PagedResult<OrderList>>(`/order-lists?pageSize=200${status ? `&status=${status}` : ''}`),
    select: (page) => [...page.items].sort((a, b) => b.generatedAt.localeCompare(a.generatedAt)),
  });
}

export function useOrderList(orderListId: string) {
  return useQuery({
    queryKey: queryKeys.orderList(orderListId),
    queryFn: () => apiFetch<OrderList>(`/order-lists/${orderListId}`),
  });
}

//one list per supplier over the period (FR-28). the server has five seconds for a december (NFR-08)
export function useGenerateOrderLists() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (period: { from: string; to: string }) =>
      apiFetch<GenerateOrderListsResponse>('/order-lists/generate', { method: 'POST', json: period }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.orderLists }),
  });
}

export type OrderListStep = 'submit' | 'approve' | 'mark-placed';

//draft, then waiting for approval, approved and placed. the approver can't be whoever generated
//it (FR-30), the api turns that away too
export function useOrderListStep() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ list, step }: { list: OrderList; step: OrderListStep }) =>
      apiFetch<OrderList>(`/order-lists/${list.orderListId}/${step}`, {
        method: 'POST',
        json: { rowVersion: list.rowVersion },
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.orderList(saved.orderListId), saved);
      void queryClient.invalidateQueries({ queryKey: queryKeys.orderLists });
    },
  });
}
