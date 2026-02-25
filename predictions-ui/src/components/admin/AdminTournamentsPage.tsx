import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import * as adminTournamentApi from '../../api/adminTournamentApi';
import type { TournamentResponse } from '../../types';
import ConfirmDialog from '../common/ConfirmDialog';
import { formatDate } from '../../utils/formatDate';
import styles from './AdminTournamentsPage.module.css';

export default function AdminTournamentsPage() {
  const [tournaments, setTournaments] = useState<TournamentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const navigate = useNavigate();

  const load = () => {
    setLoading(true);
    adminTournamentApi.getAll().then(setTournaments).finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    setEditingId(null);
    setName('');
    setShowForm(true);
  };

  const openEdit = (t: TournamentResponse) => {
    setEditingId(t.id);
    setName(t.name);
    setShowForm(true);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (editingId) {
      await adminTournamentApi.update(editingId, { name });
    } else {
      await adminTournamentApi.create({ name });
    }
    setShowForm(false);
    load();
  };

  const handleDelete = async (id: number) => {
    await adminTournamentApi.remove(id);
    setPendingDeleteId(null);
    load();
  };

  if (loading) return <div className={styles.loading}>Loading...</div>;

  return (
    <div>
      <div className={styles.header}>
        <h1>Manage Tournaments</h1>
        <button className={styles.createBtn} onClick={openCreate}>
          + New Tournament
        </button>
      </div>

      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {tournaments.map((t) => (
              <tr key={t.id}>
                <td>{t.name}</td>
                <td>{formatDate(t.createdAt)}</td>
                <td>
                  <div className={styles.actions}>
                    <button
                      className={styles.manageBtn}
                      onClick={() => navigate(`/admin/tournaments/${t.id}/games`)}
                    >
                      Games
                    </button>
                    <button className={styles.editBtn} onClick={() => openEdit(t)}>
                      Edit
                    </button>
                    <button className={styles.deleteBtn} onClick={() => setPendingDeleteId(t.id)}>
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {pendingDeleteId !== null && (
        <ConfirmDialog
          message="Delete this tournament? All games and predictions will be removed."
          onConfirm={() => handleDelete(pendingDeleteId)}
          onCancel={() => setPendingDeleteId(null)}
        />
      )}

      {showForm && (
        <div className={styles.formOverlay}>
          <div className={styles.formCard}>
            <h2>{editingId ? 'Edit Tournament' : 'New Tournament'}</h2>
            <form onSubmit={handleSubmit}>
              <div>
                <label>Name</label>
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <div className={styles.formActions}>
                <button
                  type="button"
                  className={styles.cancelBtn}
                  onClick={() => setShowForm(false)}
                >
                  Cancel
                </button>
                <button type="submit" className={styles.saveBtn}>
                  {editingId ? 'Save' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
