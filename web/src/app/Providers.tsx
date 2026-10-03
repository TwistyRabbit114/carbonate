import { useState, type ReactNode } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '@/auth/AuthProvider';
import { createQueryClient } from './queryClient';

type ProvidersProps = {
  children: ReactNode;
};

export function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}
