import { useEffect, useState, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { getTournament } from '../../api/tournamentApi';
import { getGames } from '../../api/gameApi';
import { getMyPredictions } from '../../api/predictionApi';
import { getStandings } from '../../api/standingsApi';
import type {
  TournamentResponse,
  GameResponse,
  PredictionResponse,
  StandingEntryResponse,
} from '../../types';
import GameCard from '../games/GameCard';
import StandingsTable from '../standings/StandingsTable';
import styles from './TournamentDetailPage.module.css';

type SortOption = 'date' | 'homeTeam' | 'awayTeam';
type FilterOption = 'today' | 'week' | 'all';

export default function TournamentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const tournamentId = Number(id);
  const [tournament, setTournament] = useState<TournamentResponse | null>(null);
  const [games, setGames] = useState<GameResponse[]>([]);
  const [myPredictions, setMyPredictions] = useState<PredictionResponse[]>([]);
  const [standings, setStandings] = useState<StandingEntryResponse[]>([]);
  const [activeTab, setActiveTab] = useState<'games' | 'standings'>('games');
  const [loading, setLoading] = useState(true);
  const [sortBy, setSortBy] = useState<SortOption>('date');
  const [filter, setFilter] = useState<FilterOption>('today');

  const loadData = () => {
    setLoading(true);
    Promise.all([
      getTournament(tournamentId),
      getGames(tournamentId),
      getMyPredictions(tournamentId),
      getStandings(tournamentId).catch(() => [] as typeof standings),
    ])
      .then(([t, g, p, s]) => {
        setTournament(t);
        setGames(g);
        setMyPredictions(p);
        setStandings(s);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();
  }, [tournamentId]);

  const filteredAndSortedGames = useMemo(() => {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const endOfToday = new Date(startOfToday.getTime() + 24 * 60 * 60 * 1000);
    const endOfWeek = new Date(startOfToday.getTime() + 7 * 24 * 60 * 60 * 1000);

    const filtered = games.filter((g) => {
      const t = new Date(g.startTime);
      if (filter === 'today') return t >= startOfToday && t < endOfToday;
      if (filter === 'week') return t >= startOfToday && t < endOfWeek;
      return true;
    });

    switch (sortBy) {
      case 'date':
        // upcoming first for today/week, newest first for all
        filtered.sort((a, b) =>
          filter === 'all'
            ? new Date(b.startTime).getTime() - new Date(a.startTime).getTime()
            : new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
        );
        break;
      case 'homeTeam':
        filtered.sort((a, b) => a.homeTeam.localeCompare(b.homeTeam));
        break;
      case 'awayTeam':
        filtered.sort((a, b) => a.awayTeam.localeCompare(b.awayTeam));
        break;
    }
    return filtered;
  }, [games, sortBy, filter]);

  if (loading) return <div className={styles.loading}>Loading...</div>;
  if (!tournament) return <div className={styles.empty}>Tournament not found.</div>;

  return (
    <div>
      <div className={styles.header}>
        {tournament.emblemUrl && (
          <img src={tournament.emblemUrl} alt="" className={styles.emblem} />
        )}
        <h1>{tournament.name}</h1>
      </div>

      <div className={styles.tabs}>
        <button
          className={activeTab === 'games' ? styles.tabActive : styles.tab}
          onClick={() => setActiveTab('games')}
        >
          Games
        </button>
        <button
          className={activeTab === 'standings' ? styles.tabActive : styles.tab}
          onClick={() => setActiveTab('standings')}
        >
          Standings
        </button>
      </div>

      {activeTab === 'games' && (
        <>
          {games.length > 0 && (
            <div className={styles.sortBar}>
              <div className={styles.filterGroup}>
                {(['today', 'week', 'all'] as FilterOption[]).map((f) => (
                  <button
                    key={f}
                    className={filter === f ? styles.filterBtnActive : styles.filterBtn}
                    onClick={() => setFilter(f)}
                  >
                    {f === 'today' ? 'Today' : f === 'week' ? 'Next 7 days' : 'All'}
                  </button>
                ))}
              </div>
              <label>Sort:</label>
              <select value={sortBy} onChange={(e) => setSortBy(e.target.value as SortOption)}>
                <option value="date">Date</option>
                <option value="homeTeam">Home Team (A-Z)</option>
                <option value="awayTeam">Away Team (A-Z)</option>
              </select>
            </div>
          )}
          {filteredAndSortedGames.length === 0 ? (
            <div className={styles.empty}>No games for this period.</div>
          ) : (
            filteredAndSortedGames.map((game) => (
              <GameCard
                key={game.id}
                game={game}
                myPrediction={myPredictions.find((p) => p.gameId === game.id) || null}
                onPredictionPlaced={(prediction) =>
                  setMyPredictions((prev) => {
                    const idx = prev.findIndex((p) => p.gameId === prediction.gameId);
                    if (idx >= 0) {
                      const next = [...prev];
                      next[idx] = prediction;
                      return next;
                    }
                    return [...prev, prediction];
                  })
                }
              />
            ))
          )}
        </>
      )}

      {activeTab === 'standings' && <StandingsTable standings={standings} />}
    </div>
  );
}
