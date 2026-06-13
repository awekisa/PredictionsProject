import { useEffect, useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { getGame } from '../../api/gameApi';
import type { GameResponse } from '../../types';
import GamePredictionsDetail from './GamePredictionsDetail';
import styles from './GamePredictionsPage.module.css';

export default function GamePredictionsPage() {
  const { tournamentId, gameId } = useParams<{ tournamentId?: string; gameId: string }>();
  const location = useLocation();
  const [game, setGame] = useState<GameResponse | null>((location.state as { game?: GameResponse } | null)?.game ?? null);
  const [loading, setLoading] = useState(!game);
  const navigate = useNavigate();

  useEffect(() => {
    if (game || !tournamentId || !gameId) return;
    getGame(Number(tournamentId), Number(gameId))
      .then(setGame)
      .finally(() => setLoading(false));
  }, [game, gameId, tournamentId]);

  if (loading) return <div className={styles.loading}>Loading predictions...</div>;
  if (!game) return <div className={styles.empty}>Game not found.</div>;

  return (
    <div>
      <button className={styles.backLink} onClick={() => navigate(-1)}>
        &larr; Back
      </button>
      <GamePredictionsDetail game={game} />
    </div>
  );
}
