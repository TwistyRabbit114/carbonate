import { usePermissions } from '@/auth/AuthContext';
import { usesDeskLayout } from '../navigation';
import { CrewLayout } from './CrewLayout';
import { DeskLayout } from './DeskLayout';

export function AppShell() {
  return usesDeskLayout(usePermissions()) ? <DeskLayout /> : <CrewLayout />;
}
