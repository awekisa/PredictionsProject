import { useEffect, useState } from 'react';
import { getUserPredictionDetails, getGlobalUserPredictionDetails } from '../../api/standingsApi';
import type { StandingEntryResponse, UserPredictionDetailResponse } from '../../types';
import styles from './StandingsTable.module.css';

interface Props {
  standings: StandingEntryResponse[];
  tournamentId?: number;
}

type DetailsByUser = Record<string, UserPredictionDetailResponse[]>;

function predictionOutcome(points: number): 'score' | 'outcome' | 'missed' {
  if (points === 3) return 'score';
  if (points === 1) return 'outcome';
  return 'missed';
}

function outcomeClass(outcome: 'score' | 'outcome' | 'missed'): string {
  if (outcome === 'score') return styles.scoreOutcome;
  if (outcome === 'outcome') return styles.correctOutcome;
  return styles.missedOutcome;
}

function PredictionResultRow({ detail }: { detail: UserPredictionDetailResponse }) {
  const outcome = predictionOutcome(detail.pointsEarned);

  return (
    <div className={`${styles.detailRow} ${outcomeClass(outcome)}`} data-testid="prediction-detail-row" data-outcome={outcome}>
      <span
        className={styles.actualResult}
        data-testid="actual-result"
        aria-label={`${detail.homeTeam} ${detail.actualHome}:${detail.actualAway} ${detail.awayTeam}`}
      >
        <span className={styles.detailTeam}>
          <span className={styles.detailTeamName}>
            <span className={styles.teamFull}>{detail.homeTeam}</span>
            {detail.homeTeamShort && <span className={styles.teamShort}>{detail.homeTeamShort}</span>}
          </span>
        </span>
        <span className={styles.detailScore}>
          {detail.actualHome}:{detail.actualAway}
        </span>
        <span className={styles.detailTeam}>
          <span className={styles.detailTeamName}>
            <span className={styles.teamFull}>{detail.awayTeam}</span>
            {detail.awayTeamShort && <span className={styles.teamShort}>{detail.awayTeamShort}</span>}
          </span>
        </span>
      </span>
      <span className={styles.predictionMeta} data-testid="prediction-meta">
        <span className={`${styles.detailScore} ${styles.predictionScore}`} data-testid="prediction-score">
          {detail.predictedHome}:{detail.predictedAway}
        </span>
        <span data-testid="points-earned" className={styles.pointsEarned}>
          {detail.pointsEarned}
        </span>
      </span>
    </div>
  );
}

export default function StandingsTable({ standings, tournamentId }: Props) {
  const [detailsByUser, setDetailsByUser] = useState<DetailsByUser>({});
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadPredictionRows() {
      if (standings.length === 0) {
        setDetailsByUser({});
        return;
      }

      setLoading(true);
      try {
        const entries = await Promise.all(
          standings.map(async (standing) => {
            const details = tournamentId
              ? await getUserPredictionDetails(tournamentId, standing.userDisplayName, 'total')
              : await getGlobalUserPredictionDetails(standing.userDisplayName, 'total');
            return [standing.userDisplayName, details] as const;
          })
        );

        if (active) {
          setDetailsByUser(Object.fromEntries(entries));
        }
      } catch {
        if (active) {
          setDetailsByUser({});
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadPredictionRows();

    return () => {
      active = false;
    };
  }, [standings, tournamentId]);

  if (standings.length === 0) {
    return <div className={styles.empty}>No standings data yet.</div>;
  }

  return (
    <>
      <div className={styles.resultSections}>
        {loading ? (
          <div className={styles.panelLoading}>Loading predictions...</div>
        ) : (
          standings.map((standing) => {
            const details = detailsByUser[standing.userDisplayName] ?? [];
            return (
              <section
                key={standing.userDisplayName}
                className={styles.resultSection}
                data-testid="prediction-result-section"
              >
                <div className={styles.resultSectionHeader}>
                  <span>{standing.userDisplayName}</span>
                  <span>{standing.points} pts</span>
                </div>
                <div className={styles.panelList}>
                  <div className={styles.detailHeader} data-testid="prediction-detail-header" aria-hidden="true">
                    <span>Result</span>
                    <span>Prediction</span>
                    <span>Pts</span>
                  </div>
                  {details.length === 0 ? (
                    <div className={styles.panelEmpty}>No predictions found.</div>
                  ) : (
                    details.map((detail, index) => <PredictionResultRow key={index} detail={detail} />)
                  )}
                </div>
              </section>
            );
          })
        )}
      </div>

      <div className={styles.note}>
        Scoring: Exact score = 3 pts | Correct outcome = 1 pt
      </div>
    </>
  );
}
