import { useState } from 'react';
import type { CompetitionStandingsResponse, GameResponse, StandingGroupResponse } from '../../types';
import TeamCrest from '../common/TeamCrest';
import { parseApiDateTime } from '../../utils/localTime';
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

type DerivedStandingRow = {
  team: string;
  played: number;
  won: number;
  draw: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
};

type DerivedGroup = {
  label: string;
  teams: string[];
  games: number;
  firstKickoff: number;
  standings: DerivedStandingRow[];
};

const WORLD_CUP_COMPETITION_ID = 2000;

type KnockoutMatchSchema = {
  matchNumber: number;
  round: string;
  kickoffUtc: string;
  homeSeed: string;
  awaySeed: string;
};

const WORLD_CUP_KNOCKOUT_SCHEMA: KnockoutMatchSchema[] = [
  { matchNumber: 73, round: 'Round of 32', kickoffUtc: '2026-06-28T19:00:00Z', homeSeed: '2A', awaySeed: '2B' },
  { matchNumber: 74, round: 'Round of 32', kickoffUtc: '2026-06-29T20:30:00Z', homeSeed: '1E', awaySeed: '3ABCDF' },
  { matchNumber: 75, round: 'Round of 32', kickoffUtc: '2026-06-30T01:00:00Z', homeSeed: '1F', awaySeed: '2C' },
  { matchNumber: 76, round: 'Round of 32', kickoffUtc: '2026-06-29T17:00:00Z', homeSeed: '1C', awaySeed: '2F' },
  { matchNumber: 77, round: 'Round of 32', kickoffUtc: '2026-06-30T21:00:00Z', homeSeed: '1I', awaySeed: '3CDFGH' },
  { matchNumber: 78, round: 'Round of 32', kickoffUtc: '2026-06-30T17:00:00Z', homeSeed: '2E', awaySeed: '2I' },
  { matchNumber: 79, round: 'Round of 32', kickoffUtc: '2026-07-01T01:00:00Z', homeSeed: '1A', awaySeed: '3CEFHI' },
  { matchNumber: 80, round: 'Round of 32', kickoffUtc: '2026-07-01T16:00:00Z', homeSeed: '1L', awaySeed: '3EHIJK' },
  { matchNumber: 81, round: 'Round of 32', kickoffUtc: '2026-07-02T00:00:00Z', homeSeed: '1D', awaySeed: '3BEFIJ' },
  { matchNumber: 82, round: 'Round of 32', kickoffUtc: '2026-07-01T20:00:00Z', homeSeed: '1G', awaySeed: '3AEHIJ' },
  { matchNumber: 83, round: 'Round of 32', kickoffUtc: '2026-07-02T23:00:00Z', homeSeed: '2K', awaySeed: '2L' },
  { matchNumber: 84, round: 'Round of 32', kickoffUtc: '2026-07-02T19:00:00Z', homeSeed: '1H', awaySeed: '2J' },
  { matchNumber: 85, round: 'Round of 32', kickoffUtc: '2026-07-03T03:00:00Z', homeSeed: '1B', awaySeed: '3EFGIJ' },
  { matchNumber: 86, round: 'Round of 32', kickoffUtc: '2026-07-03T22:00:00Z', homeSeed: '1J', awaySeed: '2H' },
  { matchNumber: 87, round: 'Round of 32', kickoffUtc: '2026-07-04T01:30:00Z', homeSeed: '1K', awaySeed: '3DEIJL' },
  { matchNumber: 88, round: 'Round of 32', kickoffUtc: '2026-07-03T18:00:00Z', homeSeed: '2D', awaySeed: '2G' },
  { matchNumber: 89, round: 'Round of 16', kickoffUtc: '2026-07-04T21:00:00Z', homeSeed: 'W74', awaySeed: 'W77' },
  { matchNumber: 90, round: 'Round of 16', kickoffUtc: '2026-07-04T17:00:00Z', homeSeed: 'W73', awaySeed: 'W75' },
  { matchNumber: 91, round: 'Round of 16', kickoffUtc: '2026-07-05T20:00:00Z', homeSeed: 'W76', awaySeed: 'W78' },
  { matchNumber: 92, round: 'Round of 16', kickoffUtc: '2026-07-06T00:00:00Z', homeSeed: 'W79', awaySeed: 'W80' },
  { matchNumber: 93, round: 'Round of 16', kickoffUtc: '2026-07-06T19:00:00Z', homeSeed: 'W83', awaySeed: 'W84' },
  { matchNumber: 94, round: 'Round of 16', kickoffUtc: '2026-07-07T00:00:00Z', homeSeed: 'W81', awaySeed: 'W82' },
  { matchNumber: 95, round: 'Round of 16', kickoffUtc: '2026-07-07T16:00:00Z', homeSeed: 'W86', awaySeed: 'W88' },
  { matchNumber: 96, round: 'Round of 16', kickoffUtc: '2026-07-07T20:00:00Z', homeSeed: 'W85', awaySeed: 'W87' },
  { matchNumber: 97, round: 'Quarter-final', kickoffUtc: '2026-07-09T20:00:00Z', homeSeed: 'W89', awaySeed: 'W90' },
  { matchNumber: 98, round: 'Quarter-final', kickoffUtc: '2026-07-10T19:00:00Z', homeSeed: 'W93', awaySeed: 'W94' },
  { matchNumber: 99, round: 'Quarter-final', kickoffUtc: '2026-07-11T21:00:00Z', homeSeed: 'W91', awaySeed: 'W92' },
  { matchNumber: 100, round: 'Quarter-final', kickoffUtc: '2026-07-12T01:00:00Z', homeSeed: 'W95', awaySeed: 'W96' },
  { matchNumber: 101, round: 'Semi-final', kickoffUtc: '2026-07-14T19:00:00Z', homeSeed: 'W97', awaySeed: 'W98' },
  { matchNumber: 102, round: 'Semi-final', kickoffUtc: '2026-07-15T19:00:00Z', homeSeed: 'W99', awaySeed: 'W100' },
  { matchNumber: 103, round: 'Third-place play-off', kickoffUtc: '2026-07-18T21:00:00Z', homeSeed: 'RU101', awaySeed: 'RU102' },
  { matchNumber: 104, round: 'Final', kickoffUtc: '2026-07-19T19:00:00Z', homeSeed: 'W101', awaySeed: 'W102' },
];

function isWorldCupLike(tournamentName?: string, externalLeagueId?: number | null): boolean {
  return externalLeagueId === WORLD_CUP_COMPETITION_ID || /world cup/i.test(tournamentName ?? '');
}

function createStandingRow(team: string): DerivedStandingRow {
  return {
    team,
    played: 0,
    won: 0,
    draw: 0,
    lost: 0,
    goalsFor: 0,
    goalsAgainst: 0,
    goalDifference: 0,
    points: 0,
  };
}

function addResult(row: DerivedStandingRow, goalsFor: number, goalsAgainst: number): void {
  row.played += 1;
  row.goalsFor += goalsFor;
  row.goalsAgainst += goalsAgainst;
  row.goalDifference = row.goalsFor - row.goalsAgainst;
  if (goalsFor > goalsAgainst) {
    row.won += 1;
    row.points += 3;
  } else if (goalsFor === goalsAgainst) {
    row.draw += 1;
    row.points += 1;
  } else {
    row.lost += 1;
  }
}

function calculateStandingRows(teams: string[], games: GameResponse[]): DerivedStandingRow[] {
  const rows = new Map<string, DerivedStandingRow>();
  teams.forEach((team) => rows.set(team, createStandingRow(team)));

  games.forEach((game) => {
    if (!game.isFinished || game.homeGoals === null || game.awayGoals === null) return;
    const home = rows.get(game.homeTeam);
    const away = rows.get(game.awayTeam);
    if (!home || !away) return;

    addResult(home, game.homeGoals, game.awayGoals);
    addResult(away, game.awayGoals, game.homeGoals);
  });

  return Array.from(rows.values());
}

function sortRowsByFifaCriteria(rows: DerivedStandingRow[], games: GameResponse[]): DerivedStandingRow[] {
  const headToHeadByPoints = new Map<number, Map<string, DerivedStandingRow>>();

  function headToHeadRow(row: DerivedStandingRow): DerivedStandingRow {
    if (!headToHeadByPoints.has(row.points)) {
      const tiedTeams = rows.filter((candidate) => candidate.points === row.points).map((candidate) => candidate.team);
      headToHeadByPoints.set(row.points, new Map(calculateStandingRows(tiedTeams, games).map((candidate) => [candidate.team, candidate])));
    }
    return headToHeadByPoints.get(row.points)?.get(row.team) ?? createStandingRow(row.team);
  }

  return [...rows].sort((a, b) => {
    const points = b.points - a.points;
    if (points !== 0) return points;

    const tiedTeamCount = rows.filter((row) => row.points === a.points).length;
    if (tiedTeamCount > 1) {
      const aHeadToHead = headToHeadRow(a);
      const bHeadToHead = headToHeadRow(b);
      const headToHead =
        bHeadToHead.points - aHeadToHead.points ||
        bHeadToHead.goalDifference - aHeadToHead.goalDifference ||
        bHeadToHead.goalsFor - aHeadToHead.goalsFor;
      if (headToHead !== 0) return headToHead;
    }

    return (
      b.goalDifference - a.goalDifference ||
      b.goalsFor - a.goalsFor ||
      a.team.localeCompare(b.team)
    );
  });
}

function deriveGroupStandings(teams: string[], games: GameResponse[]): DerivedStandingRow[] {
  return sortRowsByFifaCriteria(calculateStandingRows(teams, games), games);
}

function formatGoalDifference(goalDifference: number): string {
  return goalDifference > 0 ? `+${goalDifference}` : `${goalDifference}`;
}

function formatSeed(seed: string): string {
  const winner = seed.match(/^1([A-L])$/);
  if (winner) return `Winner Group ${winner[1]}`;

  const runnerUp = seed.match(/^2([A-L])$/);
  if (runnerUp) return `Runner-up Group ${runnerUp[1]}`;

  const thirdPlace = seed.match(/^3([A-L]+)$/);
  if (thirdPlace) return `Best third-place team from Groups ${thirdPlace[1].split('').join('/')}`;

  const matchWinner = seed.match(/^W(\d+)$/);
  if (matchWinner) return `Winner Match ${matchWinner[1]}`;

  const matchRunnerUp = seed.match(/^RU(\d+)$/);
  if (matchRunnerUp) return `Runner-up Match ${matchRunnerUp[1]}`;

  return seed;
}

function normalizeTime(value: string): number {
  return parseApiDateTime(value).getTime();
}

function isSeedPlaceholder(team: string): boolean {
  return /^(Winner|Runner-up|Best third-place team)\b/i.test(team);
}

function findStoredKnockoutGame(match: KnockoutMatchSchema, games: GameResponse[]): GameResponse | undefined {
  const schemaKickoff = normalizeTime(match.kickoffUtc);
  return games.find((game) => {
    if (game.id === match.matchNumber) return true;
    return normalizeTime(game.startTime) === schemaKickoff;
  });
}

function displaySlot(storedTeam: string | undefined, seed: string): string {
  if (storedTeam && !isSeedPlaceholder(storedTeam)) return storedTeam;
  return formatSeed(seed);
}

function groupKnockoutSchema(games: GameResponse[]): { round: string; matches: (KnockoutMatchSchema & { homeTeam: string; awayTeam: string })[] }[] {
  const byRound = new Map<string, (KnockoutMatchSchema & { homeTeam: string; awayTeam: string })[]>();

  WORLD_CUP_KNOCKOUT_SCHEMA.forEach((match) => {
    const storedGame = findStoredKnockoutGame(match, games);
    const displayMatch = {
      ...match,
      homeTeam: displaySlot(storedGame?.homeTeam, match.homeSeed),
      awayTeam: displaySlot(storedGame?.awayTeam, match.awaySeed),
    };
    if (!byRound.has(match.round)) byRound.set(match.round, []);
    byRound.get(match.round)!.push(displayMatch);
  });

  return Array.from(byRound.entries()).map(([round, matches]) => ({ round, matches }));
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

    const kickoff = parseApiDateTime(game.startTime).getTime();
    firstKickoffs.set(game.homeTeam, Math.min(firstKickoffs.get(game.homeTeam) ?? kickoff, kickoff));
    firstKickoffs.set(game.awayTeam, Math.min(firstKickoffs.get(game.awayTeam) ?? kickoff, kickoff));
  });

  const seen = new Set<string>();
  const components: { teams: string[]; games: number; firstKickoff: number; standings: DerivedStandingRow[] }[] = [];

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
    const componentGames = games.filter((game) => teamSet.has(game.homeTeam) && teamSet.has(game.awayTeam));
    components.push({
      teams: teams.sort((a, b) => a.localeCompare(b)),
      games: componentGames.length,
      firstKickoff: Math.min(...teams.map((team) => firstKickoffs.get(team) ?? Number.MAX_SAFE_INTEGER)),
      standings: deriveGroupStandings(teams, componentGames),
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

function isUngroupedLeagueStandings(groups: StandingGroupResponse[]): boolean {
  return groups.length === 1 && !groups[0].group;
}

function TournamentFormatPanel({ games }: { games: GameResponse[] }) {
  const groups = deriveFixtureGroups(games);
  const knockoutRounds = groupKnockoutSchema(games);

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
              <div key={group.label} className={styles.groupCard} data-testid="world-cup-group-card">
                <div className={styles.groupCardTitle}>{group.label}</div>
                <div className={styles.groupStandingsHeader}>
                  <span>Team</span>
                  <span title="Played">P</span>
                  <span title="Goals for">GF</span>
                  <span title="Goals against">GA</span>
                  <span title="Goal difference">GD</span>
                  <span title="Points">Pts</span>
                </div>
                <div className={styles.groupTeams}>
                  {group.standings.map((row) => (
                    <div key={row.team} className={styles.groupTeamRow}>
                      <div className={styles.groupTeamIdentity}>
                        <TeamCrest teamName={row.team} className={styles.crest} />
                        <span>{row.team}</span>
                      </div>
                      <span className={styles.groupStat}>{row.played}</span>
                      <span className={styles.groupStat}>{row.goalsFor}</span>
                      <span className={styles.groupStat}>{row.goalsAgainst}</span>
                      <span className={styles.groupStat}>{formatGoalDifference(row.goalDifference)}</span>
                      <span className={styles.groupPts}>{row.points}</span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
      <div className={styles.formatSection}>
        <div className={styles.sectionHeader}>
          <span>Knockout Bracket</span>
          <span className={styles.sectionMeta}>official FIFA schema</span>
        </div>
        <p className={styles.knockoutNote}>Qualified teams replace seed labels automatically once the knockout fixtures are populated.</p>
        <div className={styles.knockoutRounds}>
          {knockoutRounds.map((round) => (
            <div key={round.round} className={styles.knockoutRound}>
              <div className={styles.knockoutRoundTitle}>{round.round}</div>
              <div className={styles.knockoutMatches}>
                {round.matches.map((match) => (
                  <div key={match.matchNumber} className={styles.knockoutMatch} data-testid="world-cup-knockout-match">
                    <div className={styles.matchNumber}>M{match.matchNumber}</div>
                    <div className={styles.knockoutTeams}>
                      <span>{match.homeTeam}</span>
                      <span className={styles.versus}>vs</span>
                      <span>{match.awayTeam}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
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
  if (showTournamentFormat && (groups.length === 0 || isUngroupedLeagueStandings(groups))) {
    return <TournamentFormatPanel games={games} />;
  }
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
