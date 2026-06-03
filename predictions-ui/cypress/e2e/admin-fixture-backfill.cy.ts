export {};

const tournaments = [
  { id: 24, name: 'FIFA World Cup', createdAt: '2026-03-06T18:34:49Z', externalLeagueId: 2000, externalSeason: 2026, emblemUrl: null },
];

describe('Admin football fixture backfill', () => {
  it('requires confirmation and shows a summary after backfilling missing fixtures', () => {
    cy.intercept('GET', '**/api/admin/tournaments', { statusCode: 200, body: tournaments });
    cy.intercept('GET', '**/api/football/status', { statusCode: 200, body: { requestsLimit: 10, requestsRemaining: 9 } });
    cy.intercept('POST', '**/api/admin/football/tournaments/24/backfill-fixtures', {
      statusCode: 200,
      body: {
        providerFixtures: 72,
        existingGames: 54,
        added: 18,
        matchedExisting: 0,
        skippedExisting: 54,
        skippedUndetermined: 0,
      },
    }).as('backfillFixtures');

    cy.visitAuthenticated('/admin/tournaments', 'Admin');
    cy.contains('FIFA World Cup').should('exist');
    cy.contains('button', 'Backfill Fixtures').click();
    cy.contains('Fetch latest fixture list and add missing games to this tournament? Existing games and predictions will be preserved.').should('exist');

    cy.contains('button', 'Cancel').click();
    cy.get('@backfillFixtures.all').should('have.length', 0);

    cy.contains('button', 'Backfill Fixtures').click();
    cy.contains('button', 'Confirm').click();
    cy.wait('@backfillFixtures');
    cy.contains('18 added, 0 matched, 54 skipped').should('exist');
  });
});
