import { useEffect, useState, useMemo } from 'react';
import { useParams, useLocation } from 'react-router-dom';
import { getTournament, getFootballStandings } from '../../api/tournamentApi';
import { getGames } from '../../api/gameApi';
import { getMyPredictions } from '../../api/predictionApi';
import { getStandings } from '../../api/standingsApi';
import type {
  TournamentResponse,
  GameResponse,
  PredictionResponse,
  StandingEntryResponse,
  CompetitionStandingsResponse,
} from '../../types';
import GameCard from '../games/GameCard';
import StandingsTable from '../standings/StandingsTable';
import FootballStandingsPanel from './FootballStandingsPanel';
import { formatDate } from '../../utils/formatDate';
import styles from './TournamentDetailPage.module.css';

type FilterOption = 'yesterday' | 'today' | 'tomorrow' | 'week' | 'all';

function getFilterDates() {
  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  const startOfYesterday = new Date(startOfToday.getTime() - 24 * 60 * 60 * 1000);
  const startOfTomorrow = new Date(startOfToday.getTime() + 24 * 60 * 60 * 1000);
  const endOfTomorrow = new Date(startOfTomorrow.getTime() + 24 * 60 * 60 * 1000);
  const endOfToday = startOfTomorrow;
  const endOfWeek = new Date(startOfToday.getTime() + 7 * 24 * 60 * 60 * 1000);
  return { startOfToday, startOfYesterday, startOfTomorrow, endOfTomorrow, endOfToday, endOfWeek };
}

export default function TournamentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const tournamentId = Number(id);

  const [tournament, setTournament] = useState<TournamentResponse | null>(
    (location.state as { tournament?: TournamentResponse } | null)?.tournament ?? null
  );
  const [games, setGames] = useState<GameResponse[]>([]);
  const [myPredictions, setMyPredictions] = useState<PredictionResponse[]>([]);
  const [standings, setStandings] = useState<StandingEntryResponse[]>([]);
  const [activeTab, setActiveTab] = useState<'games' | 'standings'>('games');
  const [gamesLoading, setGamesLoading] = useState(true);
  const [footballStandings, setFootballStandings] = useState<CompetitionStandingsResponse | null>(null);
  const [footballStandingsLoading, setFootballStandingsLoading] = useState(false);
  const [filter, setFilter] = useState<FilterOption>('today');

  useEffect(() => {
    setGamesLoading(true);
    Promise.all([
      getTournament(tournamentId),
      getGames(tournamentId),
      getMyPredictions(tournamentId),
      getStandings(tournamentId).catch(() => [] as StandingEntryResponse[]),
    ]).then(([t, g, p, s]) => {
      setTournament(t);
      setGames(g);
      setMyPredictions(p);
      setStandings(s);
      setGamesLoading(false);

      if (t?.externalLeagueId) {
        setFootballStandingsLoading(true);
        getFootballStandings(tournamentId).then((fs) => {
          setFootballStandings(fs);
          setFootballStandingsLoading(false);
        });
      }
    });
  }, [tournamentId]);

  const filteredGames = useMemo(() => {
    const { startOfToday, startOfYesterday, startOfTomorrow, endOfTomorrow, endOfToday, endOfWeek } =
      getFilterDates();

    const filtered = games.filter((g) => {
      const t = new Date(g.startTime);
      if (filter === 'yesterday') return t >= startOfYesterday && t < startOfToday;
      if (filter === 'today') return t >= startOfToday && t < endOfToday;
      if (filter === 'tomorrow') return t >= startOfTomorrow && t < endOfTomorrow;
      if (filter === 'week') return t >= startOfToday && t < endOfWeek;
      return true;
    });

    // Upcoming first for day filters, newest first for 'all'
    filtered.sort((a, b) =>
      filter === 'all'
        ? new Date(b.startTime).getTime() - new Date(a.startTime).getTime()
        : new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
    );

    return filtered;
  }, [games, filter]);

  // Dates for filter labels
  const today = new Date();
  const yesterday = new Date(today); yesterday.setDate(today.getDate() - 1);
  const tomorrow = new Date(today); tomorrow.setDate(today.getDate() + 1);

  const filterLabels: { key: FilterOption; label: string; date?: string }[] = [
    { key: 'yesterday', label: 'Yesterday', date: formatDate(yesterday) },
    { key: 'today', label: 'Today', date: formatDate(today) },
    { key: 'tomorrow', label: 'Tomorrow', date: formatDate(tomorrow) },
    { key: 'week', label: 'This Week' },
  ];

  if (!gamesLoading && !tournament) return <div className={styles.empty}>Tournament not found.</div>;

  return (
    <div>
      <div className={styles.header}>
        {tournament?.emblemUrl && (
          <img src={tournament.emblemUrl} alt="" className={styles.emblem} />
        )}
        <h1>{tournament?.name ?? ''}</h1>
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
          Prediction Standings
        </button>
      </div>

      {activeTab === 'games' && (
        <>
          {!gamesLoading && games.length > 0 && (
            <div className={styles.filterBar}>
              <div className={styles.filterGroup}>
                {filterLabels.map(({ key, label, date }) => (
                  <button
                    key={key}
                    className={filter === key ? styles.filterBtnActive : styles.filterBtn}
                    onClick={() => setFilter(key)}
                  >
                    <span className={styles.filterLabel}>{label}</span>
                    {date && <span className={styles.filterDate}>{date}</span>}
                  </button>
                ))}
              </div>
              <button
                className={filter === 'all' ? styles.filterAllActive : styles.filterAll}
                onClick={() => setFilter('all')}
              >
                All
              </button>
            </div>
          )}
          <div className={styles.gamesLayout}>
            <div className={styles.gamesList}>
              {gamesLoading ? (
                <div className={styles.gamesLoading} />
              ) : filteredGames.length === 0 ? (
                <div className={styles.empty}>No games for this period.</div>
              ) : (
                filteredGames.map((game) => (
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
            </div>
            {(footballStandingsLoading || tournament?.externalLeagueId) && (
              <div className={styles.standingsSidebar}>
                <FootballStandingsPanel
                  standings={footballStandings}
                  loading={footballStandingsLoading}
                  hasExternalLeague={!!tournament?.externalLeagueId}
                />
              </div>
            )}
          </div>
        </>
      )}

      {activeTab === 'standings' && <StandingsTable standings={standings} />}
    </div>
  );
}
