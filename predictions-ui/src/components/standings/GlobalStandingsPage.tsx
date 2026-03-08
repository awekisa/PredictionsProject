import { useEffect, useState } from 'react';
import { getGlobalStandings } from '../../api/standingsApi';
import type { StandingEntryResponse } from '../../types';
import StandingsTable from './StandingsTable';
import styles from './GlobalStandingsPage.module.css';

export default function GlobalStandingsPage() {
  const [standings, setStandings] = useState<StandingEntryResponse[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getGlobalStandings()
      .then(setStandings)
      .catch(() => setStandings([]))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <h1 className={styles.title}>Global Standings</h1>
      {loading ? (
        <div className={styles.loading} />
      ) : (
        <StandingsTable standings={standings} />
      )}
    </div>
  );
}
