import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { placePrediction } from '../../api/predictionApi';
import type { GameResponse, PredictionResponse } from '../../types';
import { formatTime } from '../../utils/formatDate';
import styles from './GameCard.module.css';

interface Props {
  game: GameResponse;
  myPrediction: PredictionResponse | null;
  onPredictionPlaced: (prediction: PredictionResponse) => void;
}

export default function GameCard({ game, myPrediction, onPredictionPlaced }: Props) {
  const [homeGoals, setHomeGoals] = useState('');
  const [awayGoals, setAwayGoals] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [editing, setEditing] = useState(false);

  const now = new Date();
  const startTime = new Date(game.startTime);
  const hasStarted = now >= startTime;
  const hasResult = game.homeGoals !== null && game.awayGoals !== null;
  const showInputs = !hasStarted && (!myPrediction || editing);

  const statusLabel = hasResult ? 'Finished' : hasStarted ? 'Live' : 'Upcoming';
  const statusClass = hasResult
    ? styles.statusFinished
    : hasStarted
      ? styles.statusLive
      : styles.statusUpcoming;

  const openEdit = () => {
    if (myPrediction) {
      setHomeGoals(String(myPrediction.homeGoals));
      setAwayGoals(String(myPrediction.awayGoals));
    }
    setEditing(true);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const prediction = await placePrediction(game.id, {
        homeGoals: Number(homeGoals),
        awayGoals: Number(awayGoals),
      });
      setEditing(false);
      onPredictionPlaced(prediction);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.card}>
      <div className={styles.timeRow}>
        <span className={styles.timeBox}>{formatTime(startTime)}</span>
        <span className={`${styles.status} ${statusClass}`}>{statusLabel}</span>
      </div>

      {showInputs ? (
        <form onSubmit={handleSubmit}>
          <div className={styles.matchRow}>
            <span className={styles.homeTeam}>
              {game.homeCrestUrl && (
                <img src={game.homeCrestUrl} alt="" className={styles.crest} />
              )}
              {game.homeTeam}
            </span>
            <div className={styles.scoreInputs}>
              <input
                type="number"
                min="0"
                placeholder="H"
                value={homeGoals}
                onChange={(e) => setHomeGoals(e.target.value)}
                required
              />
              <span className={styles.colon}>:</span>
              <input
                type="number"
                min="0"
                placeholder="A"
                value={awayGoals}
                onChange={(e) => setAwayGoals(e.target.value)}
                required
              />
            </div>
            <span className={styles.awayTeam}>
              {game.awayTeam}
              {game.awayCrestUrl && (
                <img src={game.awayCrestUrl} alt="" className={styles.crest} />
              )}
            </span>
          </div>
          <div className={styles.actionRow}>
            <button type="submit" className={styles.predictBtn} disabled={submitting}>
              {submitting ? '...' : editing ? 'Update' : 'Predict'}
            </button>
            {editing && (
              <button
                type="button"
                className={styles.cancelBtn}
                onClick={() => setEditing(false)}
              >
                Cancel
              </button>
            )}
          </div>
        </form>
      ) : (
        <>
          <div className={styles.matchRow}>
            <span className={styles.homeTeam}>
              {game.homeCrestUrl && (
                <img src={game.homeCrestUrl} alt="" className={styles.crest} />
              )}
              {game.homeTeam}
            </span>
            <span className={styles.score}>
              {hasResult ? `${game.homeGoals}:${game.awayGoals}` : '0:0'}
            </span>
            <span className={styles.awayTeam}>
              {game.awayTeam}
              {game.awayCrestUrl && (
                <img src={game.awayCrestUrl} alt="" className={styles.crest} />
              )}
            </span>
          </div>
          <div className={styles.actionRow}>
            {myPrediction && !hasStarted && (
              <button className={styles.editBtn} onClick={openEdit}>
                Edit
              </button>
            )}
            {hasStarted && (
              <Link className={styles.viewLink} to={`/games/${game.id}/predictions`}>
                View all predictions
              </Link>
            )}
          </div>
        </>
      )}
    </div>
  );
}
