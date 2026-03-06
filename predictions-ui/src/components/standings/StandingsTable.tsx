import { useState } from 'react';
import { getUserPredictionDetails } from '../../api/standingsApi';
import type { StandingEntryResponse, UserPredictionDetailResponse } from '../../types';
import styles from './StandingsTable.module.css';

interface Props {
  standings: StandingEntryResponse[];
  tournamentId: number;
}

type DetailType = 'all' | 'outcomes' | 'scores' | 'total';

interface ActivePanel {
  user: string;
  type: DetailType;
  label: string;
}

function positionClass(pos: number): string {
  if (pos === 1) return styles.gold;
  if (pos === 2) return styles.silver;
  if (pos === 3) return styles.bronze;
  return '';
}

function panelTitle(label: string, user: string): string {
  return `${user} — ${label}`;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
}

export default function StandingsTable({ standings, tournamentId }: Props) {
  const [panel, setPanel] = useState<ActivePanel | null>(null);
  const [details, setDetails] = useState<UserPredictionDetailResponse[]>([]);
  const [loading, setLoading] = useState(false);

  const openPanel = async (user: string, type: DetailType, label: string) => {
    if (panel?.user === user && panel?.type === type) {
      setPanel(null);
      return;
    }
    setPanel({ user, type, label });
    setLoading(true);
    try {
      const data = await getUserPredictionDetails(tournamentId, user, type);
      setDetails(data);
    } catch {
      setDetails([]);
    } finally {
      setLoading(false);
    }
  };

  if (standings.length === 0) {
    return <div className={styles.empty}>No standings data yet.</div>;
  }

  return (
    <>
      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>Player</th>
              <th>Points</th>
              <th>Correct Outcomes</th>
              <th>Correct Scores</th>
              <th>Total Predictions</th>
            </tr>
          </thead>
          <tbody>
            {standings.map((s) => (
              <tr key={s.position}>
                <td className={positionClass(s.position)}>{s.position}</td>
                <td>{s.userDisplayName}</td>
                <td>
                  <button
                    className={styles.cellLink}
                    onClick={() => openPanel(s.userDisplayName, 'all', 'All Scoring Predictions')}
                  >
                    {s.points}
                  </button>
                </td>
                <td>
                  <button
                    className={styles.cellLink}
                    onClick={() => openPanel(s.userDisplayName, 'outcomes', 'Correct Outcomes (1 pt)')}
                  >
                    {s.correctOutcomes}
                  </button>
                </td>
                <td>
                  <button
                    className={styles.cellLink}
                    onClick={() => openPanel(s.userDisplayName, 'scores', 'Correct Scores (3 pts)')}
                  >
                    {s.correctScores}
                  </button>
                </td>
                <td>
                  <button
                    className={styles.cellLink}
                    onClick={() => openPanel(s.userDisplayName, 'total', 'All Predictions')}
                  >
                    {s.totalPredictions}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {panel && (
        <div className={styles.detailPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelTitle}>{panelTitle(panel.label, panel.user)}</span>
            <button className={styles.panelClose} onClick={() => setPanel(null)}>
              &times;
            </button>
          </div>
          {loading ? (
            <div className={styles.panelLoading}>Loading...</div>
          ) : details.length === 0 ? (
            <div className={styles.panelEmpty}>No predictions found.</div>
          ) : (
            <div className={styles.panelList}>
              {details.map((d, i) => (
                <div key={i} className={styles.detailRow}>
                  <span className={styles.detailDate}>{formatDate(d.matchDate)}</span>
                  <span className={styles.detailTeam}>
                    {d.homeCrestUrl && <img src={d.homeCrestUrl} alt="" className={styles.detailCrest} />}
                    {d.homeTeam}
                  </span>
                  <span className={styles.detailScore}>
                    {d.predictedHome}:{d.predictedAway}
                  </span>
                  <span className={styles.detailVs}>vs</span>
                  <span className={styles.detailScore}>
                    {d.actualHome}:{d.actualAway}
                  </span>
                  <span className={styles.detailTeam + ' ' + styles.detailTeamRight}>
                    {d.awayTeam}
                    {d.awayCrestUrl && <img src={d.awayCrestUrl} alt="" className={styles.detailCrest} />}
                  </span>
                  <span
                    className={
                      d.pointsEarned === 3
                        ? styles.detailPts3
                        : d.pointsEarned === 1
                          ? styles.detailPts1
                          : styles.detailPts0
                    }
                  >
                    {d.pointsEarned > 0 ? `+${d.pointsEarned}` : '0'}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div className={styles.note}>
        Scoring: Exact score = 3 pts | Correct outcome = 1 pt
      </div>
    </>
  );
}
