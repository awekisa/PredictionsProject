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
import styles from './TournamentDetailPage.module.css';

type FilterOption = number | 'all'; // number = day offset from today (0=today, -1=yesterday, 1=tomorrow, …)

function getDateRange(offset: number) {
  const start = new Date();
  start.setHours(0, 0, 0, 0);
  start.setDate(start.getDate() + offset);
  const end = new Date(start);
  end.setDate(end.getDate() + 1);
  return { start, end };
}

function getTabInfo(offset: number): { label: string; date: string } {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  let label: string;
  if (offset === 0) label = 'Today';
  else if (offset === -1) label = 'Yesterday';
  else if (offset === 1) label = 'Tomorrow';
  else label = d.toLocaleDateString('en-GB', { weekday: 'short' });
  const date = d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
  return { label, date };
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
  const [filter, setFilter] = useState<FilterOption>(0);

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
let filtered: GameResponse[];
    if (filter === 'all') {
      filtered = [...games];
    } else {
      const { start, end } = getDateRange(filter);
      filtered = games.filter((g) => { const t = new Date(g.startTime); return t >= start && t < end; });
    }

    filtered.sort((a, b) =>
      filter === 'all'
        ? new Date(b.startTime).getTime() - new Date(a.startTime).getTime()
        : new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
    );

    return filtered;
  }, [games, filter]);

  const dayOffset = typeof filter === 'number' ? filter : 0;

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
                {[-1, 0, 1].map((rel) => {
                  const offset = dayOffset + rel;
                  const { label, date } = getTabInfo(offset);
                  const isActive = typeof filter === 'number' && filter === offset;
                  return (
                    <button
                      key={rel}
                      className={isActive ? styles.filterBtnActive : styles.filterBtn}
                      onClick={() => setFilter(offset)}
                    >
                      <span className={styles.filterLabel}>{label}</span>
                      <span className={styles.filterDate}>{date}</span>
                    </button>
                  );
                })}
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

      {activeTab === 'standings' && <StandingsTable standings={standings} tournamentId={tournamentId} />}
    </div>
  );
}
