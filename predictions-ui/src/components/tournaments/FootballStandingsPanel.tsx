import { useState } from 'react';
import type { CompetitionStandingsResponse, GameResponse, StandingGroupResponse } from '../../types';
import TeamCrest from '../common/TeamCrest';
import styles from './FootballStandingsPanel.module.css';

interface Props {
  standings: CompetitionStandingsResponse | null;
  loading: boolean;
  hasExternalLeague: boolean;
  tournamentName?: string;
  externalLeagueId?: number | null;
  games?: GameResponse[];
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
                <TeamCrest teamName={row.teamName} fallbackUrl={row.teamCrest} className={styles.crest} />
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

type DerivedGroup = {
  label: string;
  teams: string[];
  games: number;
  firstKickoff: number;
};

const WORLD_CUP_COMPETITION_ID = 2000;

function isWorldCupLike(tournamentName?: string, externalLeagueId?: number | null): boolean {
  return externalLeagueId === WORLD_CUP_COMPETITION_ID || /world cup/i.test(tournamentName ?? '');
}

function deriveFixtureGroups(games: GameResponse[]): DerivedGroup[] {
  const adjacency = new Map<string, Set<string>>();
  const firstKickoffs = new Map<string, number>();

  games.forEach((game) => {
    if (!game.homeTeam || !game.awayTeam) return;

    if (!adjacency.has(game.homeTeam)) adjacency.set(game.homeTeam, new Set());
    if (!adjacency.has(game.awayTeam)) adjacency.set(game.awayTeam, new Set());
    adjacency.get(game.homeTeam)!.add(game.awayTeam);
    adjacency.get(game.awayTeam)!.add(game.homeTeam);

    const kickoff = new Date(game.startTime).getTime();
    firstKickoffs.set(game.homeTeam, Math.min(firstKickoffs.get(game.homeTeam) ?? kickoff, kickoff));
    firstKickoffs.set(game.awayTeam, Math.min(firstKickoffs.get(game.awayTeam) ?? kickoff, kickoff));
  });

  const seen = new Set<string>();
  const components: { teams: string[]; games: number; firstKickoff: number }[] = [];

  adjacency.forEach((_edges, startTeam) => {
    if (seen.has(startTeam)) return;

    const stack = [startTeam];
    const teams: string[] = [];
    seen.add(startTeam);

    while (stack.length > 0) {
      const team = stack.pop()!;
      teams.push(team);
      adjacency.get(team)?.forEach((opponent) => {
        if (!seen.has(opponent)) {
          seen.add(opponent);
          stack.push(opponent);
        }
      });
    }

    const teamSet = new Set(teams);
    const gamesInComponent = games.filter((game) => teamSet.has(game.homeTeam) && teamSet.has(game.awayTeam)).length;
    components.push({
      teams: teams.sort((a, b) => a.localeCompare(b)),
      games: gamesInComponent,
      firstKickoff: Math.min(...teams.map((team) => firstKickoffs.get(team) ?? Number.MAX_SAFE_INTEGER)),
    });
  });

  return components
    .filter((group) => group.teams.length >= 3 && group.teams.length <= 4 && group.games >= 3)
    .sort((a, b) => a.firstKickoff - b.firstKickoff)
    .map((group, index) => ({
      ...group,
      label: `Group ${String.fromCharCode(65 + index)}`,
    }));
}

function TournamentFormatPanel({ games }: { games: GameResponse[] }) {
  const groups = deriveFixtureGroups(games);

  return (
    <div className={styles.panel}>
      <h3 className={styles.title}>Tournament Format</h3>
      <div className={styles.formatSection}>
        <div className={styles.sectionHeader}>
          <span>Group Stage</span>
          <span className={styles.sectionMeta}>{groups.length} groups</span>
        </div>
        {groups.length === 0 ? (
          <p className={styles.unavailable}>Group fixtures are not available yet.</p>
        ) : (
          <div className={styles.groupCards}>
            {groups.map((group) => (
              <div key={group.label} className={styles.groupCard}>
                <div className={styles.groupCardTitle}>{group.label}</div>
                <div className={styles.groupTeams}>
                  {group.teams.map((team) => (
                    <div key={team} className={styles.groupTeamRow}>
                      <TeamCrest teamName={team} className={styles.crest} />
                      <span>{team}</span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
      <div className={styles.formatSection}>
        <div className={styles.sectionHeader}>Knockout Bracket</div>
        <p className={styles.unavailable}>Bracket appears once knockout fixtures are available.</p>
      </div>
    </div>
  );
}

export default function FootballStandingsPanel({ standings, loading, hasExternalLeague, tournamentName, externalLeagueId, games = [] }: Props) {
  const [selectedGroup, setSelectedGroup] = useState(0);

  if (loading) return <div className={styles.panel}><div className={styles.shimmer} /></div>;
  const showTournamentFormat = isWorldCupLike(tournamentName, externalLeagueId);

  if (!standings) {
    if (showTournamentFormat) return <TournamentFormatPanel games={games} />;
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
      <h3 className={styles.title}>{showTournamentFormat || isGroupStage ? 'Tournament Format' : 'League Table'}</h3>

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
