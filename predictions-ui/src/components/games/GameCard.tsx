import { useState, type FormEvent } from 'react';
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
  const [showInputs, setShowInputs] = useState(false);

  const now = new Date();
  const startTime = new Date(game.startTime);
  const hasStarted = now >= startTime;
  const hasScore = game.homeGoals !== null && game.awayGoals !== null;

  const isCorrectPrediction =
    myPrediction !== null &&
    game.isFinished &&
    hasScore &&
    myPrediction.homeGoals === game.homeGoals &&
    myPrediction.awayGoals === game.awayGoals;

  const statusLabel = game.isFinished ? 'Finished' : hasStarted ? 'Live' : 'Upcoming';
  const statusClass = game.isFinished
    ? styles.statusFinished
    : hasStarted
      ? styles.statusLive
      : styles.statusUpcoming;

  const openPredict = () => {
    setHomeGoals('');
    setAwayGoals('');
    setShowInputs(true);
  };

  const openEdit = () => {
    if (myPrediction) {
      setHomeGoals(String(myPrediction.homeGoals));
      setAwayGoals(String(myPrediction.awayGoals));
    }
    setShowInputs(true);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const prediction = await placePrediction(game.id, {
        homeGoals: Number(homeGoals),
        awayGoals: Number(awayGoals),
      });
      setShowInputs(false);
      onPredictionPlaced(prediction);
    } finally {
      setSubmitting(false);
    }
  };

  const scoreDisplay = hasScore ? `${game.homeGoals} : ${game.awayGoals}` : '– : –';

  return (
    <div className={styles.card}>
      {/* Row 1: action badge (left) | time (center) | status badge (right) */}
      <div className={styles.topRow}>
        <div className={styles.actionArea}>
          {/* Upcoming: no prediction yet */}
          {!hasStarted && !myPrediction && !showInputs && (
            <button className={styles.predictBadge} onClick={openPredict} data-cy="predict-btn">
              Predict
            </button>
          )}

          {/* Upcoming: input form open */}
          {!hasStarted && showInputs && (
            <form className={styles.inputForm} onSubmit={handleSubmit}>
              <input
                className={styles.scoreInput}
                type="number"
                min="0"
                max="99"
                value={homeGoals}
                onChange={(e) => setHomeGoals(e.target.value)}
                required
              />
              <span className={styles.inputColon}>:</span>
              <input
                className={styles.scoreInput}
                type="number"
                min="0"
                max="99"
                value={awayGoals}
                onChange={(e) => setAwayGoals(e.target.value)}
                required
              />
              <button type="submit" className={styles.setBtn} disabled={submitting}>
                {submitting ? '…' : 'Set'}
              </button>
            </form>
          )}

          {/* Upcoming: prediction set, not editing */}
          {!hasStarted && myPrediction && !showInputs && (
            <>
              <span className={styles.predictedBadge}>
                Predicted&nbsp; {myPrediction.homeGoals}:{myPrediction.awayGoals}
              </span>
              <button className={styles.editBtn} onClick={openEdit}>
                Edit
              </button>
            </>
          )}

          {/* Started: correct prediction */}
          {hasStarted && isCorrectPrediction && (
            <span className={styles.predictedCorrectBadge}>Predicted!</span>
          )}

          {/* Started: prediction exists but not correct (or game not finished yet) */}
          {hasStarted && myPrediction && !isCorrectPrediction && (
            <span className={styles.predictedBadge}>
              Predicted&nbsp; {myPrediction.homeGoals}:{myPrediction.awayGoals}
            </span>
          )}

          {/* Started: no prediction */}
          {hasStarted && !myPrediction && (
            <span className={styles.noPredictionBadge}>No Prediction</span>
          )}
        </div>

        <span className={styles.timeBox}>{formatTime(startTime)}</span>
        <span className={`${styles.statusBadge} ${statusClass}`}>{statusLabel}</span>
      </div>

      {/* Row 2: HomeTeam + flag | score | flag + AwayTeam */}
      <div className={styles.matchRow}>
        <span className={styles.homeTeam}>
          {game.homeTeam}
          {game.homeCrestUrl && (
            <img src={game.homeCrestUrl} alt="" className={styles.crest} />
          )}
        </span>
        <span className={styles.score}>{scoreDisplay}</span>
        <span className={styles.awayTeam}>
          {game.awayCrestUrl && (
            <img src={game.awayCrestUrl} alt="" className={styles.crest} />
          )}
          {game.awayTeam}
        </span>
      </div>
    </div>
  );
}
