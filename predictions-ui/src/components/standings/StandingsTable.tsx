import { useState } from 'react';
import { getUserPredictionDetails, getGlobalUserPredictionDetails } from '../../api/standingsApi';
import type { StandingEntryResponse, UserPredictionDetailResponse } from '../../types';
import styles from './StandingsTable.module.css';

interface Props {
  standings: StandingEntryResponse[];
  tournamentId?: number;
}

function medalClass(position: number): string {
  if (position === 1) return styles.gold;
  if (position === 2) return styles.silver;
  if (position === 3) return styles.bronze;
  return '';
}

function predictionOutcomeClass(points: number): string {
  if (points === 3) return styles.scoreOutcome;
  if (points === 1) return styles.correctOutcome;
  return styles.missedOutcome;
}

function predictionOutcomeLabel(points: number): 'exact-score' | 'correct-outcome' | 'missed' {
  if (points === 3) return 'exact-score';
  if (points === 1) return 'correct-outcome';
  return 'missed';
}

function PlayerPredictionsDetail({
  playerName,
  details,
  loading,
  onClose,
}: {
  playerName: string;
  details: UserPredictionDetailResponse[];
  loading: boolean;
  onClose: () => void;
}) {
  return (
    <section className={styles.detailPanel} data-testid="player-predictions-detail">
      <div className={styles.detailPanelHeader}>
        <h2>{playerName} predictions</h2>
        <button className={styles.closeButton} onClick={onClose} aria-label="Close player predictions">×</button>
      </div>
      {loading ? (
        <div className={styles.panelEmpty}>Loading predictions...</div>
      ) : details.length === 0 ? (
        <div className={styles.panelEmpty}>No started-game predictions found.</div>
      ) : (
        <div className={styles.playerDetailList}>
          {details.map((detail, index) => (
            <div
              className={`${styles.playerDetailRow} ${predictionOutcomeClass(detail.pointsEarned)}`}
              data-outcome={predictionOutcomeLabel(detail.pointsEarned)}
              data-testid="prediction-detail-row"
              key={`${detail.homeTeam}-${detail.awayTeam}-${index}`}
            >
              <span className={styles.actualResult}>{detail.homeTeam} {detail.actualHome}:{detail.actualAway} {detail.awayTeam}</span>
              <span className={styles.predictionScore}>Predicted {detail.predictedHome}:{detail.predictedAway}</span>
              <strong className={styles.pointsEarned}>{detail.pointsEarned} pts</strong>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

export default function StandingsTable({ standings, tournamentId }: Props) {
  const [selectedPlayer, setSelectedPlayer] = useState<string | null>(null);
  const [selectedDetails, setSelectedDetails] = useState<UserPredictionDetailResponse[]>([]);
  const [loadingDetails, setLoadingDetails] = useState(false);

  if (standings.length === 0) {
    return <div className={styles.empty}>No standings data yet.</div>;
  }

  const openPlayer = async (playerName: string) => {
    setSelectedPlayer(playerName);
    setSelectedDetails([]);
    setLoadingDetails(true);
    try {
      const details = tournamentId
        ? await getUserPredictionDetails(tournamentId, playerName, 'total')
        : await getGlobalUserPredictionDetails(playerName, 'total');
      setSelectedDetails(details);
    } finally {
      setLoadingDetails(false);
    }
  };

  return (
    <>
      <div className={styles.tableWrap} data-testid="prediction-leaderboard">
        <table className={styles.leaderboardTable}>
          <colgroup>
            <col className={styles.rankColumn} />
            <col className={styles.playerColumn} />
            <col className={styles.metricColumn} />
            <col className={styles.metricColumn} />
            <col className={styles.metricColumn} />
            <col className={styles.metricColumn} />
            <col className={styles.metricColumn} />
          </colgroup>
          <thead>
            <tr>
              <th data-short="#">Rank #</th>
              <th data-short="Player">Player Name</th>
              <th data-short="CS">CS</th>
              <th data-short="1X2">1X2</th>
              <th data-short="PG">PG</th>
              <th data-short="#TP">#TP</th>
              <th data-short="PTS">PTS</th>
            </tr>
          </thead>
          <tbody>
            {standings.map((standing) => (
              <tr
                key={standing.userDisplayName}
                className={styles.clickableRow}
                data-testid="prediction-leaderboard-row"
                onClick={() => openPlayer(standing.userDisplayName)}
                tabIndex={0}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    openPlayer(standing.userDisplayName);
                  }
                }}
              >
                <td className={medalClass(standing.position)}>{standing.position}</td>
                <td>{standing.userDisplayName}</td>
                <td className={styles.centered}>{standing.correctScores}</td>
                <td className={styles.centered}>{standing.correctOutcomes}</td>
                <td className={styles.centered}>{standing.correctScores + standing.correctOutcomes}</td>
                <td className={styles.centered}>{standing.totalPredictions}</td>
                <td className={styles.centered}>{standing.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.note}>Exact score = 3 pts | Correct outcome = 1 pt</div>
      <div className={styles.legend}>CS – Correct score | 1X2 – Correct outcome | PG – Games with points won | #TP – Total number of predictions | PTS – Total points</div>

      {selectedPlayer && (
        <div className={styles.modalBackdrop} role="dialog" aria-modal="true">
          <div className={styles.modalPanel}>
            <PlayerPredictionsDetail
              playerName={selectedPlayer}
              details={selectedDetails}
              loading={loadingDetails}
              onClose={() => setSelectedPlayer(null)}
            />
          </div>
        </div>
      )}
    </>
  );
}
