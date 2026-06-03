const tournament = {
  id: 1,
  name: 'World Cup 2026',
  createdAt: '2026-01-01T00:00:00Z',
  externalLeagueId: 2000,
  externalSeason: 2026,
  emblemUrl: null,
};

const standings = [
  {
    position: 1,
    userDisplayName: 'Admin',
    points: 0,
    correctOutcomes: 0,
    correctScores: 0,
    totalPredictions: 1,
  },
];

const predictionDetails = [
  {
    homeTeam: 'Jordan',
    awayTeam: 'Argentina',
    homeCrestUrl: null,
    awayCrestUrl: null,
    homeTeamShort: 'JOR',
    awayTeamShort: 'ARG',
    predictedHome: 1,
    predictedAway: 2,
    actualHome: 0,
    actualAway: 0,
    pointsEarned: 0,
    matchDate: '2026-06-03T12:58:12Z',
  },
];

describe('Standings prediction detail panel', () => {
  it('shows actual match result on the left and right-aligns prediction plus points', () => {
    cy.viewport(390, 844);
    cy.intercept('GET', '**/api/tournaments/1', { statusCode: 200, body: tournament });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: standings });
    cy.intercept('GET', '**/api/tournaments/1/football-standings', { statusCode: 204, body: '' });
    cy.intercept('GET', '**/api/tournaments/1/standings/Admin/predictions?type=total', {
      statusCode: 200,
      body: predictionDetails,
    }).as('details');

    cy.visitAuthenticated('/tournaments/1');
    cy.contains('button', 'Prediction Standings').click();
    cy.contains('button', '1').click();
    cy.wait('@details');

    cy.contains('Admin — All Predictions').should('exist');
    cy.get('[data-testid="prediction-detail-row"]').within(() => {
      cy.get('[data-testid="actual-result"]')
        .should('have.attr', 'aria-label', 'Jordan 0:0 Argentina')
        .and('be.visible');
      cy.get('[data-testid="actual-result"] [class*="teamShort"]').should('be.visible');
      cy.get('[data-testid="actual-result"] [class*="teamFull"]').should('not.be.visible');
      cy.get('[data-testid="prediction-score"]').should('have.text', '1:2').and('be.visible');
      cy.get('[data-testid="points-earned"]').should('have.text', '0').and('be.visible');
    });

    cy.get('[data-testid="prediction-detail-row"]').then(($row) => {
      const rowRect = $row[0].getBoundingClientRect();
      cy.get('[data-testid="actual-result"]').then(($actual) => {
        const actualRect = $actual[0].getBoundingClientRect();
        expect(actualRect.left).to.be.closeTo(rowRect.left, 16);
      });
      cy.get('[data-testid="prediction-meta"]').then(($meta) => {
        const metaRect = $meta[0].getBoundingClientRect();
        expect(metaRect.right).to.be.closeTo(rowRect.right, 16);
        expect(metaRect.left).to.be.greaterThan(rowRect.left + rowRect.width * 0.55);
      });
    });
  });
});
