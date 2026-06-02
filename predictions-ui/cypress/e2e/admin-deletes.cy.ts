const tournaments = [
  { id: 1, name: 'Delete Cup', createdAt: '2025-01-01T00:00:00Z', externalLeagueId: null, externalSeason: null, emblemUrl: null },
];

const users = [
  { id: 'u1', email: 'alice@test.com', displayName: 'Alice', roles: ['User'], predictionCount: 2 },
  { id: 'admin1', email: 'admin@test.com', displayName: 'Admin User', roles: ['Admin'], predictionCount: 1 },
];

const predictions = [
  {
    id: 10,
    gameId: 5,
    homeTeam: 'Arsenal',
    awayTeam: 'Chelsea',
    homeGoals: 2,
    awayGoals: 1,
    userDisplayName: 'Alice',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

describe('Admin destructive deletes', () => {
  it('requires confirmation before deleting a tournament', () => {
    cy.intercept('GET', '**/api/admin/tournaments', { statusCode: 200, body: tournaments });
    cy.intercept('GET', '**/api/football/status', { statusCode: 200, body: { requestsLimit: 100, requestsRemaining: 100 } });
    cy.intercept('DELETE', '**/api/admin/tournaments/1', { statusCode: 204 }).as('deleteTournament');

    cy.visitAuthenticated('/admin/tournaments', 'Admin');
    cy.contains('Delete Cup').should('exist');
    cy.contains('button', 'Delete').click();
    cy.contains('Delete this tournament? All games and predictions will be removed.').should('exist');
    cy.contains('button', 'Cancel').click();
    cy.get('@deleteTournament.all').should('have.length', 0);

    cy.contains('button', 'Delete').click();
    cy.contains('button', 'Confirm').click();
    cy.wait('@deleteTournament');
  });

  it('lets admins delete individual predictions only after confirmation', () => {
    cy.intercept('GET', '**/api/admin/predictions', { statusCode: 200, body: predictions });
    cy.intercept('DELETE', '**/api/admin/predictions/10', { statusCode: 204 }).as('deletePrediction');

    cy.visitAuthenticated('/admin/predictions', 'Admin');
    cy.contains('Alice').should('exist');
    cy.contains('Arsenal').should('exist');
    cy.contains('button', 'Delete').click();
    cy.contains('Delete this prediction?').should('exist');
    cy.contains('button', 'Confirm').click();
    cy.wait('@deletePrediction');
  });

  it('lets admins delete users only after confirmation', () => {
    cy.intercept('GET', '**/api/admin/users', { statusCode: 200, body: users });
    cy.intercept('DELETE', '**/api/admin/users/u1', { statusCode: 204 }).as('deleteUser');

    cy.visitAuthenticated('/admin/users', 'Admin');
    cy.contains('Alice').should('exist');
    cy.contains('alice@test.com').should('exist');
    cy.contains('button', 'Delete').click();
    cy.contains('Delete user Alice? Their predictions will also be removed.').should('exist');
    cy.contains('button', 'Confirm').click();
    cy.wait('@deleteUser');
  });
});
