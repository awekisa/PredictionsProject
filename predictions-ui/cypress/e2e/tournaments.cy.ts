describe('Tournament list', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/api/tournaments', {
      statusCode: 200,
      body: [
        { id: 1, name: 'Premier League', createdAt: '2025-01-01T00:00:00Z' },
        { id: 2, name: 'Champions Cup', createdAt: '2025-02-01T00:00:00Z' },
      ],
    }).as('getTournaments');

    cy.visit('/');
    cy.loginAs('User');
    cy.visit('/tournaments');
    cy.wait('@getTournaments');
  });

  it('displays tournament names', () => {
    cy.contains('Premier League').should('exist');
    cy.contains('Champions Cup').should('exist');
  });

  it('navigates to tournament detail on click', () => {
    cy.intercept('GET', '**/api/tournaments/1', {
      statusCode: 200,
      body: { id: 1, name: 'Premier League', createdAt: '2025-01-01T00:00:00Z' },
    }).as('getTournament');

    cy.intercept('GET', '**/api/tournaments/1/games*', {
      statusCode: 200,
      body: [],
    }).as('getGames');

    cy.contains('Premier League').click();
    cy.url().should('include', '/tournaments/1');
  });
});

describe('Tournament list - empty state', () => {
  it('shows empty message when no tournaments', () => {
    cy.intercept('GET', '**/api/tournaments', {
      statusCode: 200,
      body: [],
    }).as('getEmpty');

    cy.visit('/');
    cy.loginAs('User');
    cy.visit('/tournaments');
    cy.wait('@getEmpty');

    cy.contains(/no tournaments/i).should('exist');
  });
});
