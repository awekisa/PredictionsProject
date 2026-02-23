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

  const loadData = () => {
    setLoading(true);
    Promise.all([
      getTournament(tournamentId),
      getGames(tournamentId),
      getMyPredictions(tournamentId),
      getStandings(tournamentId),
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

  const sortedGames = useMemo(() => {
    const sorted = [...games];
    switch (sortBy) {
      case 'date':
        sorted.sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime());
        break;
      case 'homeTeam':
        sorted.sort((a, b) => a.homeTeam.localeCompare(b.homeTeam));
        break;
      case 'awayTeam':
        sorted.sort((a, b) => a.awayTeam.localeCompare(b.awayTeam));
        break;
    }
    return sorted;
  }, [games, sortBy]);

  if (loading) return <div className={styles.loading}>Loading...</div>;
  if (!tournament) return <div className={styles.empty}>Tournament not found.</div>;

  return (
    <div>
      <div className={styles.header}>
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
              <label>Sort by:</label>
              <select value={sortBy} onChange={(e) => setSortBy(e.target.value as SortOption)}>
                <option value="date">Start Date (newest)</option>
                <option value="homeTeam">Home Team (A-Z)</option>
                <option value="awayTeam">Away Team (A-Z)</option>
              </select>
            </div>
          )}
          {games.length === 0 ? (
            <div className={styles.empty}>No games yet.</div>
          ) : (
            sortedGames.map((game) => (
              <GameCard
                key={game.id}
                game={game}
                myPrediction={myPredictions.find((p) => p.gameId === game.id) || null}
                onPredictionPlaced={loadData}
              />
            ))
          )}
        </>
      )}

      {activeTab === 'standings' && <StandingsTable standings={standings} />}
    </div>
  );
}
