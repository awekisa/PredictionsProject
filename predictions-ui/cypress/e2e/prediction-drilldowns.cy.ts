const tournament = {
  id: 1,
  name: 'World Cup 2026',
  createdAt: '2026-01-01T00:00:00Z',
  externalLeagueId: 2000,
  externalSeason: 2026,
  emblemUrl: null,
};

const finishedGame = {
  id: 101,
  tournamentId: 1,
  homeTeam: 'Brazil',
  awayTeam: 'Germany',
  startTime: '2026-06-14T18:00:00Z',
  homeGoals: 2,
  awayGoals: 1,
  isFinished: true,
  homeCrestUrl: null,
  awayCrestUrl: null,
  homeTeamShort: 'BRA',
  awayTeamShort: 'GER',
};

const futureGame = {
  id: 102,
  tournamentId: 1,
  homeTeam: 'France',
  awayTeam: 'Spain',
  startTime: '2027-06-16T18:00:00Z',
  homeGoals: null,
  awayGoals: null,
  isFinished: false,
  homeCrestUrl: null,
  awayCrestUrl: null,
  homeTeamShort: 'FRA',
  awayTeamShort: 'ESP',
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

const playerDetails = [
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
    matchDate: '2026-06-14T18:00:00Z',
  },
];

const gamePredictions = [
  {
    id: 1,
    gameId: 101,
    homeTeam: 'Brazil',
    awayTeam: 'Germany',
    homeGoals: 2,
    awayGoals: 1,
    userDisplayName: 'Mitko',
    createdAt: '2026-06-13T10:00:00Z',
  },
  {
    id: 2,
    gameId: 101,
    homeTeam: 'Brazil',
    awayTeam: 'Germany',
    homeGoals: 1,
    awayGoals: 0,
    userDisplayName: 'Alex',
    createdAt: '2026-06-13T11:00:00Z',
  },
];

function stubTournament() {
  cy.intercept('GET', '**/api/tournaments/1', { statusCode: 200, body: tournament });
  cy.intercept('GET', '**/api/tournaments/1/games', { statusCode: 200, body: [finishedGame, futureGame] });
  cy.intercept('GET', '**/api/tournaments/1/my-predictions', { statusCode: 200, body: [] });
  cy.intercept('GET', '**/api/tournaments/1/standings', { statusCode: 200, body: standings });
  cy.intercept('GET', '**/api/tournaments/1/football-standings', { statusCode: 204, body: '' });
}

describe('prediction standings and drill-downs', () => {
  it('shows a leaderboard-only standings table and opens player predictions from a row click', () => {
    stubTournament();
    cy.intercept('GET', '**/api/tournaments/1/standings/Mitko/predictions?type=total', {
      statusCode: 200,
      body: playerDetails,
    }).as('playerDetails');

    cy.visitAuthenticated('/tournaments/1');
    cy.contains('button', 'Prediction Standings').click();

    cy.get('[data-testid="prediction-leaderboard"]').within(() => {
      cy.get('thead th').then(($ths) => {
        expect([...$ths].map((th) => th.textContent?.trim())).to.deep.equal([
          'Rank #',
          'Player Name',
          'Exact Scores',
          'Correct Outcomes',
          'Games with Points',
          'Total Predicted Games',
          'Total Points',
        ]);
      });
      cy.contains('td', 'Mitko').should('be.visible');
      cy.contains('td', '1').should('be.visible');
      cy.contains('td', '4').should('be.visible');
    });

    cy.contains('Exact score = 3 pts | Correct outcome = 1 pt').should('be.visible');
    cy.get('[data-testid="prediction-result-section"]').should('not.exist');
    cy.get('[data-testid="prediction-detail-row"]').should('not.exist');

    cy.contains('[data-testid="prediction-leaderboard-row"]', 'Mitko').click();
    cy.wait('@playerDetails');
    cy.get('[data-testid="player-predictions-detail"]').within(() => {
      cy.contains('Mitko predictions').should('be.visible');
      cy.contains('Brazil 2:1 Germany').should('be.visible');
      cy.contains('Predicted 2:1').should('be.visible');
      cy.contains('3 pts').should('be.visible');
      cy.contains('France').should('not.exist');
    });
  });

  it('keeps every standings column inside a narrow mobile viewport', () => {
    cy.viewport(320, 720);
    stubTournament();

    cy.visitAuthenticated('/tournaments/1');
    cy.contains('button', 'Prediction Standings').click();

    cy.get('[data-testid="prediction-leaderboard"]').then(($leaderboard) => {
      const el = $leaderboard[0];
      expect(el.scrollWidth, 'leaderboard width').to.be.at.most(el.clientWidth + 1);
    });

    cy.get('[data-testid="prediction-leaderboard"] thead th').should('have.length', 7).each(($th) => {
      const rect = $th[0].getBoundingClientRect();
      expect(rect.left, `${$th.text()} left edge`).to.be.at.least(0);
      expect(rect.right, `${$th.text()} right edge`).to.be.at.most(320);
    });
  });

  it('opens all player predictions by clicking a finished fixture score and leaves upcoming scores non-clickable', () => {
    stubTournament();
    cy.intercept('GET', '**/api/games/101/predictions', { statusCode: 200, body: gamePredictions }).as('gamePredictions');

    cy.visitAuthenticated('/tournaments/1');
    cy.contains('button', 'All').click();

    cy.get('[data-testid="game-score-button"][data-game-id="101"]').should('be.visible').click();
    cy.wait('@gamePredictions');
    cy.get('[data-testid="game-predictions-detail"]').within(() => {
      cy.contains('Brazil 2:1 Germany').should('be.visible');
      cy.contains('Mitko').should('be.visible');
      cy.contains('2:1').should('be.visible');
      cy.contains('3 pts').should('be.visible');
      cy.contains('Alex').should('be.visible');
      cy.contains('1:0').should('be.visible');
      cy.contains('1 pt').should('be.visible');
    });

    cy.get('[data-testid="game-score-button"][data-game-id="102"]').should('not.exist');
  });
});
