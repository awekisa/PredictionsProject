const tournamentId = 1;
const existingUtcStart = '2026-06-11T19:00:00Z';

function visitAdminGames() {
  cy.intercept('GET', `**/api/admin/tournaments/${tournamentId}/games`, {
    statusCode: 200,
    body: [
      {
        id: 10,
        tournamentId,
        homeTeam: 'Bulgaria',
        awayTeam: 'Spain',
        startTime: existingUtcStart,
        homeGoals: null,
        awayGoals: null,
        isFinished: false,
      },
    ],
  }).as('getGames');

  cy.visitAuthenticated(`/admin/tournaments/${tournamentId}/games`, 'Admin');
  cy.wait('@getGames');
}

describe('Admin game local/UTC time handling', () => {
  it('displays UTC game starts in browser local time and sends edited local input as UTC', () => {
    visitAdminGames();

    const expectedDisplay = new Intl.DateTimeFormat('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })
      .format(new Date(existingUtcStart))
      .replace(',', '');
    const expectedLocalInput = new Date(new Date(existingUtcStart).getTime() - new Date(existingUtcStart).getTimezoneOffset() * 60000)
      .toISOString()
      .slice(0, 16);
    const editedLocalInput = '2026-06-12T22:30';
    const expectedEditedUtc = new Date(editedLocalInput).toISOString();

    cy.contains('td', expectedDisplay).should('exist');
    cy.contains('button', 'Edit').click();
    cy.get('input[type="datetime-local"]').should('have.value', expectedLocalInput);

    cy.intercept('PUT', `**/api/admin/tournaments/${tournamentId}/games/10`, (req) => {
      expect(req.body.startTime).to.equal(expectedEditedUtc);
      req.reply({
        statusCode: 200,
        body: {
          id: 10,
          tournamentId,
          homeTeam: req.body.homeTeam,
          awayTeam: req.body.awayTeam,
          startTime: req.body.startTime,
          homeGoals: null,
          awayGoals: null,
          isFinished: false,
        },
      });
    }).as('updateGame');

    cy.get('input[type="datetime-local"]').clear().type(editedLocalInput);
    cy.contains('button', 'Save').click();
    cy.wait('@updateGame');
  });
});
