export {};

const tournaments = [
  { id: 24, name: 'FIFA World Cup', createdAt: '2026-03-06T18:34:49Z', externalLeagueId: 2000, externalSeason: 2026, emblemUrl: null },
];

describe('Admin tournament external API controls', () => {
  it('hides provider import, quota, API badges, backfill, and sync controls', () => {
    cy.intercept('GET', '**/api/admin/tournaments', { statusCode: 200, body: tournaments });
    let statusRequests = 0;
    let backfillRequests = 0;
    let syncRequests = 0;
    cy.intercept('GET', '**/api/football/status', (req) => {
      statusRequests += 1;
      req.reply({ statusCode: 500, body: { error: 'external status should not be used' } });
    });
    cy.intercept('POST', '**/api/admin/football/tournaments/24/backfill-fixtures', (req) => {
      backfillRequests += 1;
      req.reply({ statusCode: 500, body: { error: 'external backfill should not be used' } });
    });
    cy.intercept('POST', '**/api/admin/football/tournaments/24/sync-scores', (req) => {
      syncRequests += 1;
      req.reply({ statusCode: 500, body: { error: 'external sync should not be used' } });
    });

    cy.visitAuthenticated('/admin/tournaments', 'Admin');

    cy.contains('FIFA World Cup').should('exist');
    cy.contains('button', 'Games').should('exist');
    cy.contains('button', 'Edit').should('exist');
    cy.contains('button', 'Delete').should('exist');
    cy.contains('button', 'Import from API').should('not.exist');
    cy.contains('button', 'Backfill Fixtures').should('not.exist');
    cy.contains('button', 'Sync Scores').should('not.exist');
    cy.contains('API req/min').should('not.exist');
    cy.contains('span', 'API').should('not.exist');
    cy.then(() => {
      expect(statusRequests).to.equal(0);
      expect(backfillRequests).to.equal(0);
      expect(syncRequests).to.equal(0);
    });
  });
});
