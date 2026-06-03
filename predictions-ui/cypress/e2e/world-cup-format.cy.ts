const groupStageGames = [
  { id: 1, tournamentId: 1, homeTeam: 'Argentina', awayTeam: 'Brazil', startTime: '2026-06-11T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ARG', awayTeamShort: 'BRA' },
  { id: 2, tournamentId: 1, homeTeam: 'Germany', awayTeam: 'Japan', startTime: '2026-06-11T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'GER', awayTeamShort: 'JPN' },
  { id: 3, tournamentId: 1, homeTeam: 'Argentina', awayTeam: 'Germany', startTime: '2026-06-15T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ARG', awayTeamShort: 'GER' },
  { id: 4, tournamentId: 1, homeTeam: 'Brazil', awayTeam: 'Japan', startTime: '2026-06-15T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'BRA', awayTeamShort: 'JPN' },
  { id: 5, tournamentId: 1, homeTeam: 'Argentina', awayTeam: 'Japan', startTime: '2026-06-20T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ARG', awayTeamShort: 'JPN' },
  { id: 6, tournamentId: 1, homeTeam: 'Brazil', awayTeam: 'Germany', startTime: '2026-06-20T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'BRA', awayTeamShort: 'GER' },
  { id: 7, tournamentId: 1, homeTeam: 'Spain', awayTeam: 'France', startTime: '2026-06-12T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ESP', awayTeamShort: 'FRA' },
  { id: 8, tournamentId: 1, homeTeam: 'England', awayTeam: 'Portugal', startTime: '2026-06-12T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ENG', awayTeamShort: 'POR' },
  { id: 9, tournamentId: 1, homeTeam: 'Spain', awayTeam: 'England', startTime: '2026-06-16T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ESP', awayTeamShort: 'ENG' },
  { id: 10, tournamentId: 1, homeTeam: 'France', awayTeam: 'Portugal', startTime: '2026-06-16T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'FRA', awayTeamShort: 'POR' },
  { id: 11, tournamentId: 1, homeTeam: 'Spain', awayTeam: 'Portugal', startTime: '2026-06-21T19:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'ESP', awayTeamShort: 'POR' },
  { id: 12, tournamentId: 1, homeTeam: 'France', awayTeam: 'England', startTime: '2026-06-21T22:00:00Z', homeGoals: null, awayGoals: null, isFinished: false, homeCrestUrl: null, awayCrestUrl: null, homeTeamShort: 'FRA', awayTeamShort: 'ENG' },
];

describe('World Cup tournament format panel', () => {
  it('shows groups and knockout placeholder instead of one league table when provider standings are unavailable', () => {
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: { id: 1, name: 'World Cup 2026', createdAt: '2026-01-01T00:00:00Z', externalLeagueId: 2000, externalSeason: 2026, emblemUrl: null },
    });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: groupStageGames });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });
    let footballStandingsRequests = 0;
    cy.intercept('GET', '**/api/tournaments/1/football-standings', (req) => {
      footballStandingsRequests += 1;
      req.reply({ statusCode: 204, body: '' });
    });

    cy.visitAuthenticated('/tournaments/1');

    cy.contains('Tournament Format').should('exist');
    cy.then(() => expect(footballStandingsRequests).to.equal(0));
    cy.contains('League Table').should('not.exist');
    cy.contains('Group Stage').should('exist');
    cy.contains('Group A').should('exist');
    cy.contains('Argentina').should('exist');
    cy.contains('Brazil').should('exist');
    cy.contains('Group B').should('exist');
    cy.contains('Spain').should('exist');
    cy.contains('France').should('exist');
    cy.contains('Knockout Bracket').should('exist');
    cy.contains('Bracket appears once knockout fixtures are available.').should('exist');
  });

  it('ignores provider one-table World Cup standings and still shows fixture-derived groups', () => {
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: { id: 1, name: 'World Cup 2026', createdAt: '2026-01-01T00:00:00Z', externalLeagueId: 2000, externalSeason: 2026, emblemUrl: null },
    });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: groupStageGames });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/football-standings', {
      statusCode: 200,
      body: {
        groups: [
          {
            group: null,
            stage: 'GROUP_STAGE',
            table: [
              { position: 1, teamName: 'Argentina', teamCrest: null, playedGames: 0, won: 0, draw: 0, lost: 0, points: 0, goalsFor: 0, goalsAgainst: 0, goalDifference: 0 },
              { position: 2, teamName: 'Brazil', teamCrest: null, playedGames: 0, won: 0, draw: 0, lost: 0, points: 0, goalsFor: 0, goalsAgainst: 0, goalDifference: 0 },
              { position: 3, teamName: 'Spain', teamCrest: null, playedGames: 0, won: 0, draw: 0, lost: 0, points: 0, goalsFor: 0, goalsAgainst: 0, goalDifference: 0 },
              { position: 4, teamName: 'France', teamCrest: null, playedGames: 0, won: 0, draw: 0, lost: 0, points: 0, goalsFor: 0, goalsAgainst: 0, goalDifference: 0 },
            ],
          },
        ],
      },
    });

    cy.visitAuthenticated('/tournaments/1');

    cy.contains('Tournament Format').should('exist');
    cy.contains('Group Stage').should('exist');
    cy.contains('Group A').should('exist');
    cy.contains('Group B').should('exist');
    cy.contains('Knockout Bracket').should('exist');
    cy.contains('table').should('not.exist');
  });
});
