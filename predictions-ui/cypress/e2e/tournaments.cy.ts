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

  it('does not call the external football standings API for league tournaments', () => {
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
    let footballStandingsRequests = 0;
    cy.intercept('GET', '**/api/tournaments/1/football-standings', (req) => {
      footballStandingsRequests += 1;
      req.reply({ statusCode: 500, body: { error: 'external API should not be used' } });
    });

    cy.visitAuthenticated('/tournaments/1');

    cy.contains('No games for this period.').should('exist');
    cy.then(() => expect(footballStandingsRequests).to.equal(0));
    cy.contains('League Table').should('not.exist');
    cy.contains('Tournament Format').should('not.exist');
  });
});
