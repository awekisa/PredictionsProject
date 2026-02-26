const futureTime = new Date(Date.now() + 86400000).toISOString();
const pastTime = new Date(Date.now() - 86400000).toISOString();

const tournament = { id: 1, name: 'Test Cup', createdAt: '2025-01-01T00:00:00Z' };

const upcomingGame = {
  id: 1, tournamentId: 1,
  homeTeam: 'Arsenal', awayTeam: 'Chelsea',
  startTime: futureTime,
  homeGoals: null, awayGoals: null, status: 'Upcoming',
};

const finishedGame = {
  id: 2, tournamentId: 1,
  homeTeam: 'Liverpool', awayTeam: 'Everton',
  startTime: pastTime,
  homeGoals: 2, awayGoals: 1, status: 'Final',
};

function visitTournament(games: object[]) {
  cy.intercept('GET', '**/api/tournaments/1', { statusCode: 200, body: tournament });
  cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: games });
  cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
  cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: [] });

  cy.visitAuthenticated('/tournaments/1');
}

describe('Predictions - upcoming game', () => {
  beforeEach(() => {
    visitTournament([upcomingGame]);
  });

  it('shows team names', () => {
    cy.contains('Arsenal').should('exist');
    cy.contains('Chelsea').should('exist');
  });

  it('shows prediction input fields', () => {
    cy.get('input[type="number"]').should('have.length.at.least', 2);
  });

  it('submits a prediction', () => {
    cy.intercept('POST', '**/api/games/1/predictions', {
      statusCode: 200,
      body: {
        id: 10, gameId: 1,
        homeTeam: 'Arsenal', awayTeam: 'Chelsea',
        homeGoals: 2, awayGoals: 1,
        userDisplayName: 'Test User',
        createdAt: new Date().toISOString(),
      },
    }).as('postPrediction');

    cy.get('input[type="number"]').eq(0).clear().type('2');
    cy.get('input[type="number"]').eq(1).clear().type('1');
    cy.contains('button', /predict/i).first().click();

    cy.wait('@postPrediction');
  });
});

describe('Predictions - finished game', () => {
  it('shows final score', () => {
    visitTournament([finishedGame]);

    cy.contains('Liverpool').should('exist');
    cy.contains('Everton').should('exist');
    cy.contains(/2.*1/).should('exist');
  });

  it('has no prediction input for finished games', () => {
    visitTournament([finishedGame]);

    cy.get('input[type="number"]').should('not.exist');
  });
});
