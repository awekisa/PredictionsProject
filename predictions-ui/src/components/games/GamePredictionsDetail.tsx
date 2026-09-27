import { useEffect, useState } from 'react';
import { getGamePredictions } from '../../api/gameApi';
import type { GameResponse, PredictionResponse } from '../../types';
import { predictionPoints } from '../../utils/predictionScoring';
import styles from './GamePredictionsDetail.module.css';

interface Props {
  game: GameResponse;
  onClose?: () => void;
}

function outcomeClass(points: number): string {
  if (points === 3) return styles.scoreOutcome;
  if (points === 1) return styles.correctOutcome;
  return styles.missedOutcome;
}

function outcomeLabel(points: number): 'exact-score' | 'correct-outcome' | 'missed' {
  if (points === 3) return 'exact-score';
  if (points === 1) return 'correct-outcome';
  return 'missed';
}

export default function GamePredictionsDetail({ game, onClose }: Props) {
  const [predictions, setPredictions] = useState<PredictionResponse[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    getGamePredictions(game.id)
      .then((rows) => {
        if (active) setPredictions(rows);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [game.id]);

  const hasScore = game.homeGoals !== null && game.awayGoals !== null;
  const actual = hasScore ? `${game.homeGoals}:${game.awayGoals}` : 'vs';

  return (
    <section className={styles.panel} data-testid="game-predictions-detail">
      <div className={styles.header}>
        <div>
          <h2>Game predictions</h2>
          <p>{game.homeTeam} {actual} {game.awayTeam}</p>
        </div>
        {onClose && <button className={styles.closeButton} onClick={onClose} aria-label="Close game predictions">×</button>}
      </div>

      {loading ? (
        <div className={styles.empty}>Loading predictions...</div>
      ) : predictions.length === 0 ? (
        <div className={styles.empty}>No predictions for this game yet.</div>
      ) : (
        <div className={styles.tableWrap}>
          <table className={styles.predictionsTable}>
            <colgroup>
              <col />
              <col className={styles.predColumn} />
              <col className={styles.ptsColumn} />
            </colgroup>
            <thead>
              <tr>
                <th>Player</th>
                <th>Pred</th>
                <th>Pts</th>
              </tr>
            </thead>
            <tbody>
              {predictions.map((prediction) => {
                const points = predictionPoints(
                  prediction.homeGoals,
                  prediction.awayGoals,
                  game.homeGoals,
                  game.awayGoals
                );
                return (
                  <tr
                    key={prediction.id}
                    className={hasScore ? outcomeClass(points) : styles.pendingOutcome}
                    data-outcome={hasScore ? outcomeLabel(points) : 'pending'}
                    data-testid="game-prediction-row"
                  >
                    <td>{prediction.userDisplayName}</td>
                    <td className={styles.predictionScore}>{prediction.homeGoals}:{prediction.awayGoals}</td>
                    <td className={styles.pointsEarned} aria-label={hasScore ? undefined : 'Points pending'}>
                      {hasScore ? `${points} pts` : '—'}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
