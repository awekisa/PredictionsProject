import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getGamePredictions } from '../../api/gameApi';
import type { PredictionResponse } from '../../types';
import { formatDateTime } from '../../utils/formatDate';
import styles from './GamePredictionsPage.module.css';

export default function GamePredictionsPage() {
  const { gameId } = useParams<{ gameId: string }>();
  const [predictions, setPredictions] = useState<PredictionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    getGamePredictions(Number(gameId))
      .then(setPredictions)
      .finally(() => setLoading(false));
  }, [gameId]);

  if (loading) return <div className={styles.loading}>Loading predictions...</div>;

  const first = predictions[0];

  return (
    <div>
      <button className={styles.backLink} onClick={() => navigate(-1)}>
        &larr; Back
      </button>

      <div className={styles.header}>
        <h1>Predictions</h1>
        {first && (
          <div className={styles.matchInfo}>
            {first.homeTeam} vs {first.awayTeam}
          </div>
        )}
      </div>

      {predictions.length === 0 ? (
        <div className={styles.empty}>No predictions for this game yet.</div>
      ) : (
        <div className={styles.tableWrap}>
          <table>
            <thead>
              <tr>
                <th>Player</th>
                <th>Prediction</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              {predictions.map((p) => (
                <tr key={p.id}>
                  <td>{p.userDisplayName}</td>
                  <td>
                    {p.homeGoals} - {p.awayGoals}
                  </td>
                  <td>{formatDateTime(p.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
