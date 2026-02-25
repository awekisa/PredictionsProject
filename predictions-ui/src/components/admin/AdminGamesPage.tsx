import { useEffect, useState, type FormEvent } from 'react';
import { useParams, Link } from 'react-router-dom';
import * as adminGameApi from '../../api/adminGameApi';
import type { GameResponse } from '../../types';
import ConfirmDialog from '../common/ConfirmDialog';
import styles from './AdminGamesPage.module.css';

type FormMode = 'none' | 'game' | 'result';

export default function AdminGamesPage() {
  const { tournamentId } = useParams<{ tournamentId: string }>();
  const tId = Number(tournamentId);
  const [games, setGames] = useState<GameResponse[]>([]);
  const [loading, setLoading] = useState(true);

  const [formMode, setFormMode] = useState<FormMode>('none');
  const [editingGameId, setEditingGameId] = useState<number | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);

  // Game form fields
  const [homeTeam, setHomeTeam] = useState('');
  const [awayTeam, setAwayTeam] = useState('');
  const [startTime, setStartTime] = useState('');

  // Result form fields
  const [homeGoals, setHomeGoals] = useState('');
  const [awayGoals, setAwayGoals] = useState('');

  const load = () => {
    setLoading(true);
    adminGameApi.getAll(tId).then(setGames).finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, [tId]);

  const closeForm = () => {
    setFormMode('none');
    setEditingGameId(null);
  };

  const openCreateGame = () => {
    setEditingGameId(null);
    setHomeTeam('');
    setAwayTeam('');
    setStartTime('');
    setFormMode('game');
  };

  const openEditGame = (g: GameResponse) => {
    setEditingGameId(g.id);
    setHomeTeam(g.homeTeam);
    setAwayTeam(g.awayTeam);
    setStartTime(g.startTime.slice(0, 16));
    setFormMode('game');
  };

  const openSetResult = (g: GameResponse) => {
    setEditingGameId(g.id);
    setHomeGoals(g.homeGoals?.toString() ?? '');
    setAwayGoals(g.awayGoals?.toString() ?? '');
    setFormMode('result');
  };

  const handleGameSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const data = { homeTeam, awayTeam, startTime };
    if (editingGameId) {
      await adminGameApi.update(tId, editingGameId, data);
    } else {
      await adminGameApi.create(tId, data);
    }
    closeForm();
    load();
  };

  const handleResultSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (editingGameId === null) return;
    await adminGameApi.setResult(editingGameId, {
      homeGoals: Number(homeGoals),
      awayGoals: Number(awayGoals),
    });
    closeForm();
    load();
  };

  const handleDelete = async (gameId: number) => {
    await adminGameApi.remove(tId, gameId);
    setPendingDeleteId(null);
    load();
  };

  if (loading) return <div className={styles.loading}>Loading...</div>;

  return (
    <div>
      <Link className={styles.backLink} to="/admin/tournaments">
        &larr; Back to Tournaments
      </Link>

      <div className={styles.header}>
        <h1>Manage Games</h1>
        <button className={styles.createBtn} onClick={openCreateGame}>
          + New Game
        </button>
      </div>

      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Home</th>
              <th>Away</th>
              <th>Start Time</th>
              <th>Score</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {games.map((g) => (
              <tr key={g.id}>
                <td>{g.homeTeam}</td>
                <td>{g.awayTeam}</td>
                <td>{new Date(g.startTime).toLocaleString()}</td>
                <td className={styles.scoreCell}>
                  {g.homeGoals !== null ? `${g.homeGoals} - ${g.awayGoals}` : '-'}
                </td>
                <td>
                  <div className={styles.actions}>
                    {new Date() >= new Date(g.startTime) && (
                      <button className={styles.resultBtn} onClick={() => openSetResult(g)}>
                        Set Result
                      </button>
                    )}
                    <button className={styles.editBtn} onClick={() => openEditGame(g)}>
                      Edit
                    </button>
                    <button className={styles.deleteBtn} onClick={() => setPendingDeleteId(g.id)}>
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
          message="Delete this game and all its predictions?"
          onConfirm={() => handleDelete(pendingDeleteId)}
          onCancel={() => setPendingDeleteId(null)}
        />
      )}

      {formMode === 'game' && (
        <div className={styles.formOverlay}>
          <div className={styles.formCard} onClick={(e) => e.stopPropagation()}>
            <h2>{editingGameId ? 'Edit Game' : 'New Game'}</h2>
            <form onSubmit={handleGameSubmit}>
              <div>
                <label>Home Team</label>
                <input
                  value={homeTeam}
                  onChange={(e) => setHomeTeam(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <div>
                <label>Away Team</label>
                <input
                  value={awayTeam}
                  onChange={(e) => setAwayTeam(e.target.value)}
                  required
                />
              </div>
              <div>
                <label>Start Time</label>
                <input
                  type="datetime-local"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  required
                />
              </div>
              <div className={styles.formActions}>
                <button type="button" className={styles.cancelBtn} onClick={closeForm}>
                  Cancel
                </button>
                <button type="submit" className={styles.saveBtn}>
                  {editingGameId ? 'Save' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {formMode === 'result' && (
        <div className={styles.formOverlay}>
          <div className={styles.formCard} onClick={(e) => e.stopPropagation()}>
            <h2>Set Game Result</h2>
            <form onSubmit={handleResultSubmit}>
              <div className={styles.scoreRow}>
                <div>
                  <label>Home Goals</label>
                  <input
                    type="number"
                    min="0"
                    value={homeGoals}
                    onChange={(e) => setHomeGoals(e.target.value)}
                    required
                    autoFocus
                  />
                </div>
                <div>
                  <label>Away Goals</label>
                  <input
                    type="number"
                    min="0"
                    value={awayGoals}
                    onChange={(e) => setAwayGoals(e.target.value)}
                    required
                  />
                </div>
              </div>
              <div className={styles.formActions}>
                <button type="button" className={styles.cancelBtn} onClick={closeForm}>
                  Cancel
                </button>
                <button type="submit" className={styles.saveBtn}>
                  Save Result
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
