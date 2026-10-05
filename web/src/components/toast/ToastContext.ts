import { createContext, useContext } from 'react';

export type ToastApi = {
  success: (message: string) => void;
  info: (message: string) => void;
  error: (message: string) => void;
};

export const ToastContext = createContext<ToastApi | null>(null);

export function useToast() {
  const value = useContext(ToastContext);
  if (!value) throw new Error('useToast needs to be inside ToastProvider');
  return value;
}
