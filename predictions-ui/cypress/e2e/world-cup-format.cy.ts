const game = (overrides: Record<string, unknown>) => ({
  id: 0,
  tournamentId: 1,
  homeTeam: 'Home',
  awayTeam: 'Away',
  startTime: '2026-06-11T19:00:00Z',
  homeGoals: null,
  awayGoals: null,
  isFinished: false,
  homeCrestUrl: null,
  awayCrestUrl: null,
  homeTeamShort: null,
  awayTeamShort: null,
  ...overrides,
});

const groupStageGames = [
  game({ id: 1, homeTeam: 'Argentina', awayTeam: 'Brazil', startTime: '2026-06-11T19:00:00Z', homeGoals: 2, awayGoals: 0, isFinished: true, homeTeamShort: 'ARG', awayTeamShort: 'BRA' }),
  game({ id: 2, homeTeam: 'Germany', awayTeam: 'Japan', startTime: '2026-06-11T22:00:00Z', homeGoals: 1, awayGoals: 1, isFinished: true, homeTeamShort: 'GER', awayTeamShort: 'JPN' }),
  game({ id: 3, homeTeam: 'Argentina', awayTeam: 'Germany', startTime: '2026-06-15T19:00:00Z', homeGoals: 0, awayGoals: 0, isFinished: true, homeTeamShort: 'ARG', awayTeamShort: 'GER' }),
  game({ id: 4, homeTeam: 'Brazil', awayTeam: 'Japan', startTime: '2026-06-15T22:00:00Z', homeGoals: 3, awayGoals: 1, isFinished: true, homeTeamShort: 'BRA', awayTeamShort: 'JPN' }),
  game({ id: 5, homeTeam: 'Argentina', awayTeam: 'Japan', startTime: '2026-06-20T19:00:00Z', homeGoals: 1, awayGoals: 2, isFinished: true, homeTeamShort: 'ARG', awayTeamShort: 'JPN' }),
  game({ id: 6, homeTeam: 'Brazil', awayTeam: 'Germany', startTime: '2026-06-20T22:00:00Z', homeGoals: 0, awayGoals: 2, isFinished: true, homeTeamShort: 'BRA', awayTeamShort: 'GER' }),
  game({ id: 7, homeTeam: 'Spain', awayTeam: 'France', startTime: '2026-06-12T19:00:00Z', homeTeamShort: 'ESP', awayTeamShort: 'FRA' }),
  game({ id: 8, homeTeam: 'England', awayTeam: 'Portugal', startTime: '2026-06-12T22:00:00Z', homeTeamShort: 'ENG', awayTeamShort: 'POR' }),
  game({ id: 9, homeTeam: 'Spain', awayTeam: 'England', startTime: '2026-06-16T19:00:00Z', homeTeamShort: 'ESP', awayTeamShort: 'ENG' }),
  game({ id: 10, homeTeam: 'France', awayTeam: 'Portugal', startTime: '2026-06-16T22:00:00Z', homeTeamShort: 'FRA', awayTeamShort: 'POR' }),
  game({ id: 11, homeTeam: 'Spain', awayTeam: 'Portugal', startTime: '2026-06-21T19:00:00Z', homeTeamShort: 'ESP', awayTeamShort: 'POR' }),
  game({ id: 12, homeTeam: 'France', awayTeam: 'England', startTime: '2026-06-21T22:00:00Z', homeTeamShort: 'FRA', awayTeamShort: 'ENG' }),
];

const knockoutGames = [
  game({ id: 73, homeTeam: 'Runner-up Group A', awayTeam: 'Runner-up Group B', startTime: '2026-06-28T21:00:00Z' }),
  game({ id: 79, homeTeam: 'Argentina', awayTeam: 'Best third-place team from Groups C/E/F/H/I', startTime: '2026-06-30T19:00:00Z', homeTeamShort: 'ARG' }),
  game({ id: 104, homeTeam: 'Winner Match 101', awayTeam: 'Winner Match 102', startTime: '2026-07-19T19:00:00Z' }),
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
    cy.get('[data-testid="world-cup-group-card"]').contains('Group A').parents('[data-testid="world-cup-group-card"]').within(() => {
      cy.contains('GF').should('exist');
      cy.contains('GA').should('exist');
      cy.contains('GD').should('exist');
      cy.contains('Pts').should('exist');
      cy.contains('[class*=groupTeamRow]', 'Germany').within(() => {
        cy.contains('[class*=groupStat]', '3').should('exist');
        cy.contains('[class*=groupStat]', '1').should('exist');
        cy.contains('[class*=groupStat]', '+2').should('exist');
        cy.contains('[class*=groupPts]', '5').should('exist');
      });
      cy.get('[class*=groupTeamRow]').then((rows) => {
        const teams = [...rows].map((row) => row.textContent ?? '');
        expect(teams[0]).to.contain('Germany');
        expect(teams[1]).to.contain('Japan');
        expect(teams[2]).to.contain('Argentina');
        expect(teams[3]).to.contain('Brazil');
      });
      cy.contains('[class*=groupTeamRow]', 'Argentina').within(() => {
        cy.contains('[class*=groupStat]', '3').should('exist');
        cy.contains('[class*=groupStat]', '2').should('exist');
        cy.contains('[class*=groupStat]', '+1').should('exist');
        cy.contains('[class*=groupPts]', '4').should('exist');
      });
    });
    cy.contains('Group B').should('exist');
    cy.contains('Spain').should('exist');
    cy.contains('France').should('exist');
    cy.contains('Knockout Bracket').should('exist');
    cy.contains('Round of 32').should('exist');
    cy.contains('M73').should('exist');
    cy.contains('Runner-up Group A').should('exist');
    cy.contains('Runner-up Group B').should('exist');
    cy.contains('M79').should('exist');
    cy.contains('Winner Group A').should('exist');
    cy.contains('Best third-place team from Groups C/E/F/H/I').should('exist');
    cy.contains('Final').should('exist');
    cy.contains('M104').should('exist');
    cy.contains('Winner Match 101').should('exist');
    cy.contains('Winner Match 102').should('exist');
  });

  it('updates knockout schema entries with real teams from stored knockout fixtures', () => {
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: { id: 1, name: 'World Cup 2026', createdAt: '2026-01-01T00:00:00Z', externalLeagueId: 2000, externalSeason: 2026, emblemUrl: null },
    });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [...groupStageGames, ...knockoutGames] });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/football-standings', { statusCode: 204, body: '' });

    cy.visitAuthenticated('/tournaments/1');

    cy.contains('[data-testid="world-cup-knockout-match"]', 'M79').within(() => {
      cy.contains('Argentina').should('exist');
      cy.contains('Best third-place team from Groups C/E/F/H/I').should('exist');
      cy.contains('Winner Group A').should('not.exist');
    });
    cy.contains('[data-testid="world-cup-knockout-match"]', 'M73').within(() => {
      cy.contains('Runner-up Group A').should('exist');
      cy.contains('Runner-up Group B').should('exist');
    });
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
