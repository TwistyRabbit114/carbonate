import { useState } from 'react';
import type { UserListItem } from '@/api/types';
import { describeRoles } from '@/auth/roles';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { Table } from '@/components/Table';
import { formatDateTime } from '@/lib/format';
import { useUsers } from './api';
import { EditUserDialog, NewUserDialog } from './UserDialog';
import styles from './SettingsPage.module.scss';

//who can sign in and what they can do (FR-38), for the director and ops. the api refuses
//anything that would leave no active director, and says so
//TODO(plan): casual crew rows should show the date their access runs out (FR-36), but the user
//list doesn't send one. ask C to add it to UserListItem
export function UsersPanel() {
  const users = useUsers();
  //undefined is closed, null is a new user
  const [editing, setEditing] = useState<UserListItem | null | undefined>(undefined);

  return (
    <Panel
      title="Users"
      actions={
        <Button variant="ghost" onClick={() => setEditing(null)}>
          Add user
        </Button>
      }
    >
      {users.isError ? (
        <ErrorState message="We couldn't load the users." onRetry={() => void users.refetch()} />
      ) : users.isPending ? (
        <p role="status">Loading the users…</p>
      ) : (
        <Table label="Everyone with a login">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Roles</th>
              <th scope="col">Status</th>
              <th scope="col">Last signed in</th>
              <th scope="col">
                <span className={styles.hidden}>Change</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {users.data.map((user) => (
              <tr key={user.userId}>
                <th scope="row">
                  {user.fullName}
                  <span className={styles.sub}>{user.email}</span>
                </th>
                <td>{describeRoles(user.roles)}</td>
                <td>
                  {user.isActive ? 'Active' : <Badge>Switched off</Badge>}
                  {user.employmentType === 'Casual' && (
                    <span className={styles.sub}>Casual: ends 7 days after their last debrief</span>
                  )}
                </td>
                <td>{user.lastLoginAt ? formatDateTime(user.lastLoginAt) : 'Never'}</td>
                <td>
                  <Button
                    variant="ghost"
                    onClick={() => setEditing(user)}
                    aria-label={`Edit ${user.fullName}`}
                  >
                    Edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {editing === null && <NewUserDialog onClose={() => setEditing(undefined)} />}
      {editing && <EditUserDialog user={editing} onClose={() => setEditing(undefined)} />}
    </Panel>
  );
}
