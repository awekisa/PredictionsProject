import { useEffect, useState } from 'react';
import * as adminDeletionApi from '../../api/adminDeletionApi';
import type { AdminPredictionResponse } from '../../types';
import ConfirmDialog from '../common/ConfirmDialog';
import { formatDateTime } from '../../utils/formatDate';
import styles from './AdminGamesPage.module.css';

export default function AdminPredictionsPage() {
  const [predictions, setPredictions] = useState<AdminPredictionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingDelete, setPendingDelete] = useState<AdminPredictionResponse | null>(null);

  useEffect(() => {
    adminDeletionApi.getPredictions().then(setPredictions).finally(() => setLoading(false));
  }, []);

  const handleDelete = async (prediction: AdminPredictionResponse) => {
    await adminDeletionApi.deletePrediction(prediction.id);
    setPendingDelete(null);
    const updated = await adminDeletionApi.getPredictions();
    setPredictions(updated);
  };

  if (loading) return <div className={styles.loading}>Loading...</div>;

  return (
    <div>
      <div className={styles.header}>
        <h1>Manage Predictions</h1>
      </div>

      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>User</th>
              <th>Game</th>
              <th>Prediction</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {predictions.map((p) => (
              <tr key={p.id}>
                <td>{p.userDisplayName}</td>
                <td>{p.homeTeam} vs {p.awayTeam}</td>
                <td>{p.homeGoals} – {p.awayGoals}</td>
                <td>{formatDateTime(p.createdAt)}</td>
                <td>
                  <button className={styles.deleteBtn} onClick={() => setPendingDelete(p)}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {predictions.length === 0 && <p>No predictions found.</p>}

      {pendingDelete && (
        <ConfirmDialog
          message="Delete this prediction?"
          onConfirm={() => handleDelete(pendingDelete)}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
