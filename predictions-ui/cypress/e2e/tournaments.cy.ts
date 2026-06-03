const tournamentList = [
  { id: 1, name: 'Premier League', createdAt: '2025-01-01T00:00:00Z' },
  { id: 2, name: 'Champions Cup', createdAt: '2025-02-01T00:00:00Z' },
];

describe('Tournament list', () => {
  it('displays tournament names', () => {
    cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: tournamentList });

    cy.visitAuthenticated('/');

    cy.contains('Premier League').should('exist');
    cy.contains('Champions Cup').should('exist');
  });

  it('shows empty message when no tournaments', () => {
    cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: [] });

    cy.visitAuthenticated('/');

    cy.contains(/no tournaments/i).should('exist');
  });

  it('navigates to tournament detail on click', () => {
    cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: tournamentList });
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: { id: 1, name: 'Premier League', createdAt: '2025-01-01T00:00:00Z' },
    });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });

    cy.visitAuthenticated('/');
    cy.contains('Premier League').click();

    cy.url().should('include', '/tournaments/1');
  });

  it('uses the South Korea flag in the league table instead of the old crest', () => {
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: {
        id: 1,
        name: 'Premier League',
        createdAt: '2025-01-01T00:00:00Z',
        externalLeagueId: 2021,
        externalSeason: 2026,
      },
    });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/football-standings', {
      statusCode: 200,
      body: {
        groups: [
          {
            group: null,
            stage: 'REGULAR_SEASON',
            table: [
              {
                position: 1,
                teamName: 'South Korea',
                teamCrest: '/crests/south-korea.svg',
                playedGames: 0,
                won: 0,
                draw: 0,
                lost: 0,
                points: 0,
                goalsFor: 0,
                goalsAgainst: 0,
                goalDifference: 0,
              },
            ],
          },
        ],
      },
    });

    cy.visitAuthenticated('/tournaments/1');

    cy.contains('td', 'South Korea')
      .find('img')
      .should('have.attr', 'src')
      .and('include', '/flags/4x3/kr.svg');
  });
});
