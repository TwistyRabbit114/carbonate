import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type {
  AuditEntry,
  CalendarConnection,
  ConnectCalendarResponse,
  CreateUserRequest,
  PagedResult,
  UpdateUserRequest,
  UserListItem,
} from '@/api/types';

//C's user admin and audit trail, and D's google calendar link

//----------------------------------------------------------\\
//                              USERS
//----------------------------------------------------------\\

//everyone, active or not, for user.manage (FR-38). a 15-person company fits on one page
export function useUsers() {
  return useQuery({
    queryKey: queryKeys.users,
    queryFn: () => apiFetch<PagedResult<UserListItem>>('/users?pageSize=200'),
    select: (page) => [...page.items].sort((a, b) => a.fullName.localeCompare(b.fullName)),
  });
}

//both refresh every list of people, the assign pickers included
export function useCreateUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (user: CreateUserRequest) => apiFetch<UserListItem>('/users', { method: 'POST', json: user }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useUpdateUser(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (change: UpdateUserRequest) =>
      apiFetch<UserListItem>(`/users/${userId}`, { method: 'PATCH', json: change }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

//----------------------------------------------------------\\
//                              AUDIT
//----------------------------------------------------------\\

export type AuditFilters = {
  entity: string; //'' for everything
  from: string; //yyyy-mm-dd, or ''
  to: string;
  page: number;
};

export const auditPageSize = 25;

//whole days in Cape Town, so "to" takes in the whole of that day
const startOfDay = (date: string) => new Date(`${date}T00:00:00+02:00`).toISOString();
const endOfDay = (date: string) => new Date(`${date}T23:59:59.999+02:00`).toISOString();

//newest first (FR-37). the old page stays up while the next one loads
export function useAudit(filters: AuditFilters) {
  const params = new URLSearchParams({ page: String(filters.page), pageSize: String(auditPageSize) });
  if (filters.entity) params.set('entity', filters.entity);
  if (filters.from) params.set('from', startOfDay(filters.from));
  if (filters.to) params.set('to', endOfDay(filters.to));

  return useQuery({
    queryKey: queryKeys.audit(filters),
    queryFn: () => apiFetch<PagedResult<AuditEntry>>(`/audit?${params}`),
    placeholderData: keepPreviousData,
  });
}

//----------------------------------------------------------\\
//                              GOOGLE CALENDAR
//----------------------------------------------------------\\

export function useCalendarConnection() {
  return useQuery({
    queryKey: queryKeys.calendarConnection,
    queryFn: () => apiFetch<CalendarConnection>('/calendar/connection'),
    retry: false,
  });
}

//the answer is google's consent page, which brings the user back to the api when they agree
export function useConnectCalendar() {
  return useMutation({
    mutationFn: () => apiFetch<ConnectCalendarResponse>('/calendar/connect', { method: 'POST' }),
  });
}

export function useDisconnectCalendar() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => apiFetch<void>('/calendar/connection', { method: 'DELETE' }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.calendarConnection }),
  });
}
