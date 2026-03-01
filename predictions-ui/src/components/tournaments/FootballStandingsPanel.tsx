import { useState } from 'react';
import type { CompetitionStandingsResponse, StandingGroupResponse } from '../../types';
import styles from './FootballStandingsPanel.module.css';

interface Props {
  standings: CompetitionStandingsResponse | null;
  loading: boolean;
  hasExternalLeague: boolean;
}

function groupLabel(group: string | null): string {
  if (!group) return '';
  // "GROUP_A" → "Group A", "GROUP_STAGE" etc.
  return group
    .split('_')
    .map((w) => w.charAt(0) + w.slice(1).toLowerCase())
    .join(' ');
}

function StandingsTable({ group }: { group: StandingGroupResponse }) {
  return (
    <table className={styles.table}>
      <colgroup>
        <col className={styles.colPos} />
        <col className={styles.colTeam} />
        <col className={styles.colNum} />
        <col className={styles.colNum} />
        <col className={styles.colNum} />
        <col className={styles.colNum} />
        <col className={styles.colGd} />
        <col className={styles.colPts} />
      </colgroup>
      <thead>
        <tr>
          <th className={styles.thPos}>#</th>
          <th className={styles.thTeam}>Team</th>
          <th title="Played">P</th>
          <th title="Won">W</th>
          <th title="Draw">D</th>
          <th title="Lost">L</th>
          <th title="Goal difference">GD</th>
          <th title="Points">Pts</th>
        </tr>
      </thead>
      <tbody>
        {group.table.map((row) => (
          <tr key={row.position}>
            <td className={styles.tdPos}>{row.position}</td>
            <td>
              <div className={styles.teamCell}>
                {row.teamCrest && (
                  <img src={row.teamCrest} alt="" className={styles.crest} />
                )}
                <span className={styles.teamName}>{row.teamName}</span>
              </div>
            </td>
            <td>{row.playedGames}</td>
            <td>{row.won}</td>
            <td>{row.draw}</td>
            <td>{row.lost}</td>
            <td>{row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}</td>
            <td className={styles.tdPts}>{row.points}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function FootballStandingsPanel({ standings, loading, hasExternalLeague }: Props) {
  const [selectedGroup, setSelectedGroup] = useState(0);

  if (loading) return <div className={styles.panel}><div className={styles.shimmer} /></div>;
  if (!standings) {
    if (!hasExternalLeague) return null;
    return (
      <div className={styles.panel}>
        <h3 className={styles.title}>League Table</h3>
        <p className={styles.unavailable}>Standings not available for this competition.</p>
      </div>
    );
  }

  const groups = standings.groups;
  if (groups.length === 0) return null;

  // Determine if this is a multi-group competition (group stage)
  const isGroupStage = groups.length > 1;

  // For group stage, show group selector. For league, show single table.
  const activeGroup = groups[Math.min(selectedGroup, groups.length - 1)];

  return (
    <div className={styles.panel}>
      <h3 className={styles.title}>League Table</h3>

      {isGroupStage && (
        <div className={styles.groupTabs}>
          {groups.map((g, i) => (
            <button
              key={i}
              className={selectedGroup === i ? styles.groupTabActive : styles.groupTab}
              onClick={() => setSelectedGroup(i)}
            >
              {groupLabel(g.group) || groupLabel(g.stage)}
            </button>
          ))}
        </div>
      )}

      <div className={styles.tableWrapper}>
        <StandingsTable group={activeGroup} />
      </div>
    </div>
  );
}
