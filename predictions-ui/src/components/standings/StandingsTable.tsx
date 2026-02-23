import type { StandingEntryResponse } from '../../types';
import styles from './StandingsTable.module.css';

interface Props {
  standings: StandingEntryResponse[];
}

function positionClass(pos: number): string {
  if (pos === 1) return styles.gold;
  if (pos === 2) return styles.silver;
  if (pos === 3) return styles.bronze;
  return '';
}

export default function StandingsTable({ standings }: Props) {
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
              <th>Correct Scores</th>
              <th>Total Predictions</th>
            </tr>
          </thead>
          <tbody>
            {standings.map((s) => (
              <tr key={s.position}>
                <td className={positionClass(s.position)}>{s.position}</td>
                <td>{s.userDisplayName}</td>
                <td>{s.points}</td>
                <td>{s.correctScores}</td>
                <td>{s.totalPredictions}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className={styles.note}>
        Scoring: Exact score = 3 pts | Correct outcome = 1 pt
      </div>
    </>
  );
}
