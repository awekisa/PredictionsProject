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
    userDisplayName: 'Mitko',
    points: 4,
    correctOutcomes: 1,
    correctScores: 1,
    totalPredictions: 3,
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
  {
    homeTeam: 'Brazil',
    awayTeam: 'Germany',
    homeCrestUrl: null,
    awayCrestUrl: null,
    homeTeamShort: 'BRA',
    awayTeamShort: 'GER',
    predictedHome: 2,
    predictedAway: 1,
    actualHome: 2,
    actualAway: 1,
    pointsEarned: 3,
    matchDate: '2026-06-04T12:58:12Z',
  },
  {
    homeTeam: 'France',
    awayTeam: 'Spain',
    homeCrestUrl: null,
    awayCrestUrl: null,
    homeTeamShort: 'FRA',
    awayTeamShort: 'ESP',
    predictedHome: 1,
    predictedAway: 0,
    actualHome: 2,
    actualAway: 1,
    pointsEarned: 1,
    matchDate: '2026-06-05T12:58:12Z',
  },
];

describe('Standings prediction result rows', () => {
  it('shows team names directly and highlights exact scores, outcomes, and misses', () => {
    cy.viewport(390, 844);
    cy.intercept('GET', '**/api/tournaments/1', { statusCode: 200, body: tournament });
    cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
    cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: standings });
    cy.intercept('GET', '**/api/tournaments/1/football-standings', { statusCode: 204, body: '' });
    cy.intercept('GET', '**/api/tournaments/1/standings/Mitko/predictions?type=total', {
      statusCode: 200,
      body: predictionDetails,
    }).as('details');

    cy.visitAuthenticated('/tournaments/1');
    cy.contains('button', 'Prediction Standings').click();
    cy.wait('@details');

    cy.contains('th', 'Player').should('not.exist');
    cy.contains('th', 'PTS').should('not.exist');
    cy.contains('th', 'OUT').should('not.exist');
    cy.contains('th', 'SCR').should('not.exist');
    cy.contains('th', 'TOT').should('not.exist');

    cy.get('[data-testid="prediction-result-section"]').within(() => {
      cy.contains('Mitko').should('be.visible');
      cy.get('[data-testid="prediction-detail-header"]').within(() => {
        cy.contains('Result').should('be.visible');
        cy.contains('Prediction').should('be.visible');
        cy.contains('Pts').should('be.visible');
      });
    });

    const expectedRows = [
      {
        result: 'Jordan 0:0 Argentina',
        home: 'Jordan',
        away: 'Argentina',
        prediction: '1:2',
        points: '0',
        outcome: 'missed',
        color: 'rgb(149, 161, 177)',
      },
      {
        result: 'Brazil 2:1 Germany',
        home: 'Brazil',
        away: 'Germany',
        prediction: '2:1',
        points: '3',
        outcome: 'score',
        color: 'rgb(73, 211, 0)',
      },
      {
        result: 'France 2:1 Spain',
        home: 'France',
        away: 'Spain',
        prediction: '1:0',
        points: '1',
        outcome: 'outcome',
        color: 'rgb(255, 152, 0)',
      },
    ];

    cy.get('[data-testid="prediction-detail-row"]').should('have.length', expectedRows.length);
    expectedRows.forEach((expected, index) => {
      cy.get('[data-testid="prediction-detail-row"]')
        .eq(index)
        .should('have.attr', 'data-outcome', expected.outcome)
        .and('have.css', 'border-left-color', expected.color)
        .within(() => {
          cy.get('[data-testid="actual-result"]')
            .should('have.attr', 'aria-label', expected.result)
            .and('be.visible')
            .and('contain.text', expected.home)
            .and('contain.text', expected.away);
          cy.get('[data-testid="actual-result"] [class*="teamFull"]').should('be.visible');
          cy.get('[data-testid="actual-result"] [class*="teamShort"]').should('not.be.visible');
          cy.get('[data-testid="prediction-score"]')
            .should('have.text', expected.prediction)
            .and('have.css', 'color', expected.color)
            .and('be.visible');
          cy.get('[data-testid="points-earned"]')
            .should('have.text', expected.points)
            .and('have.css', 'color', expected.color)
            .and('be.visible');
        });
    });

    cy.get('[data-testid="prediction-detail-row"]').first().then(($row) => {
      const rowRect = $row[0].getBoundingClientRect();
      cy.get('[data-testid="actual-result"]').first().then(($actual) => {
        const actualRect = $actual[0].getBoundingClientRect();
        expect(actualRect.left).to.be.closeTo(rowRect.left, 20);
      });
      cy.get('[data-testid="prediction-meta"]').first().then(($meta) => {
        const metaRect = $meta[0].getBoundingClientRect();
        expect(metaRect.right).to.be.closeTo(rowRect.right, 16);
        expect(metaRect.left).to.be.greaterThan(rowRect.left + rowRect.width * 0.55);
      });
    });
  });
});
