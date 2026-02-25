import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { placePrediction } from '../../api/predictionApi';
import type { GameResponse, PredictionResponse } from '../../types';
import styles from './GameCard.module.css';

interface Props {
  game: GameResponse;
  myPrediction: PredictionResponse | null;
  onPredictionPlaced: () => void;
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
  const showForm = !hasStarted && (!myPrediction || editing);

  const statusLabel = hasResult ? 'Final' : hasStarted ? 'Live' : 'Upcoming';
  const statusClass = hasResult
    ? styles.statusFinal
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
      await placePrediction(game.id, {
        homeGoals: Number(homeGoals),
        awayGoals: Number(awayGoals),
      });
      setEditing(false);
      onPredictionPlaced();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.card}>
      <div className={styles.metaRow}>
        <span className={styles.dateCol}>{startTime.toLocaleString()}</span>
        <span className={`${styles.status} ${statusClass}`}>{statusLabel}</span>
      </div>

      <div className={styles.matchRow}>
        <span className={styles.homeTeam}>{game.homeTeam}</span>
        {hasResult ? (
          <span className={styles.score}>
            {game.homeGoals} - {game.awayGoals}
          </span>
        ) : (
          <span className={styles.vs}>vs</span>
        )}
        <span className={styles.awayTeam}>{game.awayTeam}</span>
      </div>

      <div className={styles.bottomRow}>
        <div className={styles.predictionCol}>
          {myPrediction && !editing && (
            <>
              <span className={styles.predictionScore}>
                {myPrediction.homeGoals} - {myPrediction.awayGoals}
              </span>
              {!hasStarted && (
                <button className={styles.editPredBtn} onClick={openEdit}>
                  Edit
                </button>
              )}
            </>
          )}
          {showForm && (
            <form className={styles.inlineForm} onSubmit={handleSubmit}>
              <input
                type="number"
                min="0"
                placeholder="H"
                value={homeGoals}
                onChange={(e) => setHomeGoals(e.target.value)}
                required
              />
              <span>-</span>
              <input
                type="number"
                min="0"
                placeholder="A"
                value={awayGoals}
                onChange={(e) => setAwayGoals(e.target.value)}
                required
              />
              <button className={styles.predictBtn} type="submit" disabled={submitting}>
                {submitting ? '...' : editing ? 'Update' : 'Predict'}
              </button>
              {editing && (
                <button
                  type="button"
                  className={styles.cancelEditBtn}
                  onClick={() => setEditing(false)}
                >
                  Cancel
                </button>
              )}
            </form>
          )}
        </div>

        <div className={styles.actionsCol}>
          {hasStarted && (
            <Link className={styles.viewLink} to={`/games/${game.id}/predictions`}>
              View predictions
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
