import { QueryClient } from '@tanstack/react-query';
import { isApiError } from '@/api/problem';

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: true,
        //a 4xx won't change on a retry, only network blips and server errors get one more go
        retry: (failureCount, error) =>
          failureCount < 1 && !(isApiError(error) && error.status >= 400 && error.status < 500),
      },
      mutations: { retry: 0 },
    },
  });
}
