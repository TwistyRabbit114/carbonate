import { useSignedIn } from '@/auth/AuthContext';
import { describeRoles } from '@/auth/roles';
import { FieldList, FieldRow } from '@/components/FieldList';
import { PageHead } from '@/components/PageHead';
import { Panel } from '@/components/Panel';
import { ChangePasswordForm } from './ChangePasswordForm';
import styles from './AccountPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//anyone signed in, reached from their own name in the menu. the details are what /api/me says,
//only someone with user.manage can change them
export default function AccountPage() {
  const me = useSignedIn();

  return (
    <>
      <PageHead title="Your account" />

      <Panel title="Details">
        <FieldList>
          <FieldRow label="Name">{me.user.fullName}</FieldRow>
          <FieldRow label="Email">{me.user.email}</FieldRow>
          <FieldRow label={me.roles.length > 1 ? 'Roles' : 'Role'}>{describeRoles(me.roles)}</FieldRow>
        </FieldList>
        <p className={styles.note}>Your Director or Operations Manager can change your name or role.</p>
      </Panel>

      <Panel title="Change password">
        <ChangePasswordForm />
      </Panel>
    </>
  );
}
