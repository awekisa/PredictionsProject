import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getTournaments } from '../../api/tournamentApi';
import type { TournamentResponse } from '../../types';
import { formatDate } from '../../utils/formatDate';
import styles from './TournamentListPage.module.css';

export default function TournamentListPage() {
  const [tournaments, setTournaments] = useState<TournamentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    getTournaments()
      .then(setTournaments)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className={styles.loading}>Loading tournaments...</div>;

  return (
    <div>
      <div className={styles.header}>
        <h1>Tournaments</h1>
      </div>
      {tournaments.length === 0 ? (
        <div className={styles.empty}>No tournaments yet.</div>
      ) : (
        <div className={styles.grid}>
          {tournaments.map((t) => (
            <div
              key={t.id}
              className={styles.card}
              onClick={() => navigate(`/tournaments/${t.id}`, { state: { tournament: t } })}
            >
              {t.emblemUrl && (
                <img src={t.emblemUrl} alt="" className={styles.emblem} />
              )}
              <div className={styles.cardName}>{t.name}</div>
              <div className={styles.cardDate}>
                Created {formatDate(t.createdAt)}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
