import { useEffect, useState } from 'react';
import * as adminDeletionApi from '../../api/adminDeletionApi';
import type { AdminUserResponse } from '../../types';
import ConfirmDialog from '../common/ConfirmDialog';
import styles from './AdminGamesPage.module.css';

export default function AdminUsersPage() {
  const [users, setUsers] = useState<AdminUserResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingDelete, setPendingDelete] = useState<AdminUserResponse | null>(null);

  useEffect(() => {
    adminDeletionApi.getUsers().then(setUsers).finally(() => setLoading(false));
  }, []);

  const handleDelete = async (user: AdminUserResponse) => {
    await adminDeletionApi.deleteUser(user.id);
    setPendingDelete(null);
    const updated = await adminDeletionApi.getUsers();
    setUsers(updated);
  };

  if (loading) return <div className={styles.loading}>Loading...</div>;

  return (
    <div>
      <div className={styles.header}>
        <h1>Manage Users</h1>
      </div>

      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Roles</th>
              <th>Predictions</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.displayName}</td>
                <td>{u.email}</td>
                <td>{u.roles.join(', ') || 'User'}</td>
                <td>{u.predictionCount}</td>
                <td>
                  <button className={styles.deleteBtn} onClick={() => setPendingDelete(u)}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {users.length === 0 && <p>No users found.</p>}

      {pendingDelete && (
        <ConfirmDialog
          message={`Delete user ${pendingDelete.displayName}? Their predictions will also be removed.`}
          onConfirm={() => handleDelete(pendingDelete)}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
