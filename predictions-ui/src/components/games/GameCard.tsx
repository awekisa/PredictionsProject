import { useState, useRef, useEffect, type FormEvent } from 'react';
import { placePrediction } from '../../api/predictionApi';
import type { GameResponse, PredictionResponse } from '../../types';
import { formatTime } from '../../utils/formatDate';
import TeamCrest from '../common/TeamCrest';
import styles from './GameCard.module.css';

const DISPLAY_NAMES: Record<string, string> = {
  'Korea Republic': 'S. Korea',
};

function displayName(name: string): string {
  return DISPLAY_NAMES[name] ?? name;
}

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
  const homeNameRef = useRef<HTMLSpanElement>(null);
  const awayNameRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    const MAX_WIDTH = 80;
    const BASE_FONT = 16;
    const measure = () => {
      if (window.innerWidth > 480) {
        if (homeNameRef.current) homeNameRef.current.style.fontSize = '';
        if (awayNameRef.current) awayNameRef.current.style.fontSize = '';
        return;
      }
      [homeNameRef, awayNameRef].forEach((ref) => {
        const el = ref.current;
        if (!el) return;
        el.style.fontSize = `${BASE_FONT}px`;
        const natural = el.scrollWidth;
        if (natural > MAX_WIDTH) {
          el.style.fontSize = `${BASE_FONT * (MAX_WIDTH / natural)}px`;
        }
      });
    };
    measure();
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
  }, [game.homeTeam, game.awayTeam]);

  const now = new Date();
  const startTime = new Date(game.startTime);
  const hasStarted = now >= startTime;
  const hasScore = game.homeGoals !== null && game.awayGoals !== null;

  const isCorrectPrediction =
    myPrediction !== null &&
    game.isFinished &&
    hasScore &&
    Number(myPrediction.homeGoals) === Number(game.homeGoals) &&
    Number(myPrediction.awayGoals) === Number(game.awayGoals);

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

  const scoreDisplay = hasScore ? `${game.homeGoals} : ${game.awayGoals}` : 'vs';

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

      {/* Row 2: TeamName — Flag — score/vs — Flag — TeamName */}
      <div className={styles.matchRow}>
        <div className={styles.teamNameBox}>
          <span ref={homeNameRef} className={styles.homeTeam}>{displayName(game.homeTeam)}</span>
        </div>
        <TeamCrest teamName={game.homeTeam} fallbackUrl={game.homeCrestUrl} className={styles.homeCrest} />
        <span className={styles.score}>{scoreDisplay}</span>
        <TeamCrest teamName={game.awayTeam} fallbackUrl={game.awayCrestUrl} className={styles.awayCrest} />
        <div className={`${styles.teamNameBox} ${styles.teamNameBoxAway}`}>
          <span ref={awayNameRef} className={styles.awayTeam}>{displayName(game.awayTeam)}</span>
        </div>
      </div>
    </div>
  );
}
