const futureTime = new Date(Date.now() + 86400000).toISOString(); // tomorrow
const pastTime = new Date(Date.now() - 86400000).toISOString(); // yesterday

const upcomingGame = {
  id: 1,
  tournamentId: 1,
  homeTeam: 'Arsenal',
  awayTeam: 'Chelsea',
  startTime: futureTime,
  homeGoals: null,
  awayGoals: null,
  status: 'Upcoming',
};

const finishedGame = {
  id: 2,
  tournamentId: 1,
  homeTeam: 'Liverpool',
  awayTeam: 'Everton',
  startTime: pastTime,
  homeGoals: 2,
  awayGoals: 1,
  status: 'Final',
};

describe('Predictions - upcoming game', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/api/tournaments/1/games*', {
      statusCode: 200,
      body: [upcomingGame],
    }).as('getGames');

    cy.intercept('GET', '**/api/tournaments/1/predictions/mine', {
      statusCode: 200,
      body: [],
    }).as('getMyPredictions');

    cy.visit('/');
    cy.loginAs('User');
    cy.visit('/tournaments/1');
    cy.wait('@getGames');
  });

  it('shows prediction input for upcoming games', () => {
    cy.contains('Arsenal').should('exist');
    cy.contains('Chelsea').should('exist');
    cy.get('input[type="number"], input[inputmode="numeric"]').should('have.length.at.least', 2);
  });

  it('submits a prediction', () => {
    cy.intercept('POST', '**/api/games/1/predictions', {
      statusCode: 200,
      body: {
        id: 10,
        gameId: 1,
        homeTeam: 'Arsenal',
        awayTeam: 'Chelsea',
        homeGoals: 2,
        awayGoals: 1,
        userDisplayName: 'Test User',
        createdAt: new Date().toISOString(),
      },
    }).as('postPrediction');

    cy.get('input[type="number"], input[inputmode="numeric"]').eq(0).clear().type('2');
    cy.get('input[type="number"], input[inputmode="numeric"]').eq(1).clear().type('1');
    cy.contains('button', /predict|save/i).first().click();

    cy.wait('@postPrediction');
    // After submit, the prediction score should be visible
    cy.contains(/2.*1|1.*2/).should('exist');
  });
});

describe('Predictions - finished game', () => {
  it('shows final score and no prediction input', () => {
    cy.intercept('GET', '**/api/tournaments/1/games*', {
      statusCode: 200,
      body: [finishedGame],
    }).as('getGames');

    cy.intercept('GET', '**/api/tournaments/1/predictions/mine', {
      statusCode: 200,
      body: [],
    }).as('getMyPredictions');

    cy.visit('/');
    cy.loginAs('User');
    cy.visit('/tournaments/1');
    cy.wait('@getGames');

    cy.contains('Liverpool').should('exist');
    cy.contains('Everton').should('exist');
    // Score should be visible
    cy.contains(/2.*1/).should('exist');
    // No prediction input for past games
    cy.get('input[type="number"]').should('not.exist');
  });
});
