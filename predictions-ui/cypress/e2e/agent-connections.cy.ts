/// <reference types="cypress" />

const path = '**/api/auth/me/agent-connections';
const connection = {
  id: 'bf807fa0-04df-4988-821d-6b4f209db851', name: 'Codex', scopes: ['app:read'],
  createdAt: '2026-09-21T12:00:00Z', expiresAt: '2099-10-21T12:00:00Z',
  lastUsedAt: null, revokedAt: null,
};
// Synthetic credential, never a real account secret.
const secret = 'pred_mcp_cypress-only-not-a-real-credential';

function openConnections(role: 'User' | 'Admin' = 'User') {
  cy.intercept('GET', '**/api/tournaments', { body: [] });
  cy.visitAuthenticated('/', role);
  cy.get('button[aria-haspopup="dialog"]').click();
  cy.contains('summary', 'Manage agent connections').scrollIntoView().click();
  cy.wait('@connections');
}

describe('Agent connections', () => {
  it('creates with read-only defaults and reveals once without persistent browser storage', () => {
    cy.intercept('GET', path, { body: { tokens: [], canGrantAdminScopes: false } }).as('connections');
    openConnections();
    cy.contains('No agent connections yet.').scrollIntoView().should('be.visible');
    cy.get('#agent-expiry').should('have.value', '30');
    cy.get('input[value="app:read"]').should('be.checked');
    cy.get('input[value="predictions:write"]').should('not.be.checked');
    cy.contains('Manage the app').should('not.exist');
    cy.intercept('POST', path, { body: { token: secret, connection } }).as('create');
    cy.get('#agent-name').type('Codex');
    cy.contains('button', 'Create access token').click();
    cy.wait('@create').its('request.body').should('deep.equal', { name: 'Codex', expirationDays: 30, scopes: ['app:read'] });
    cy.get('#agent-new-token').should('have.value', secret);
    cy.window().then((win) => {
      expect(JSON.stringify(win.localStorage)).not.to.contain(secret);
      expect(JSON.stringify(win.sessionStorage)).not.to.contain(secret);
    });
    cy.contains('button', 'hide token').click();
    cy.get('#agent-new-token').should('not.exist');
    cy.intercept('GET', path, { body: { tokens: [connection], canGrantAdminScopes: false } }).as('connections');
    cy.get('button[aria-label="Close account settings"]').click();
    cy.get('button[aria-haspopup="dialog"]').click();
    cy.contains('summary', 'Manage agent connections').scrollIntoView().click();
    cy.wait('@connections');
    cy.get('#agent-new-token').should('not.exist');
    cy.get('ul[aria-label="Your agent connections"]').should('contain.text', 'Codex').and('contain.text', 'Never');
  });

  it('lets current admins explicitly select scopes and expiration', () => {
    cy.intercept('GET', path, { body: { tokens: [], canGrantAdminScopes: true } }).as('connections');
    openConnections('Admin');
    cy.get('input[value="admin:read"]').should('not.be.checked').check();
    cy.get('input[value="admin:write"]').should('not.be.checked').check();
    cy.get('input[value="predictions:write"]').check();
    cy.get('#agent-expiry').select('7');
    cy.get('#agent-name').type('Hermes');
    cy.intercept('POST', path, { body: { token: secret, connection: { ...connection, name: 'Hermes' } } }).as('create');
    cy.contains('button', 'Create access token').click();
    cy.wait('@create').then(({ request }) => {
      expect(request.body.expirationDays).to.equal(7);
      expect(request.body.scopes).to.have.members(['app:read', 'predictions:write', 'admin:read', 'admin:write']);
    });
    cy.get('#agent-new-token').should('exist');
    cy.contains('summary', 'Manage agent connections').click();
    cy.contains('summary', 'Manage agent connections').click();
    cy.wait('@connections');
    cy.get('#agent-new-token').should('not.exist');
  });

  it('uses live permissions rather than an old Admin browser claim', () => {
    cy.intercept('GET', path, { body: { tokens: [], canGrantAdminScopes: false } }).as('connections');
    openConnections('Admin');
    cy.get('input[value="admin:write"]').should('not.exist');
    cy.get('input[value="app:read"]').uncheck();
    cy.get('#agent-name').type('Codex');
    cy.contains('button', 'Create access token').should('be.disabled');
  });

  it('shows loading failure and supports retry', () => {
    cy.intercept('GET', path, { statusCode: 500 }).as('connections');
    openConnections();
    cy.contains('Could not load agent connections').scrollIntoView().should('be.visible');
    cy.intercept('GET', path, { body: { tokens: [], canGrantAdminScopes: false } }).as('connections');
    cy.contains('button', 'Retry loading').click();
    cy.wait('@connections');
    cy.contains('No agent connections yet.').scrollIntoView().should('be.visible');
  });

  it('reports create and revoke failures without hiding active connections', () => {
    cy.intercept('GET', path, { body: { tokens: [connection], canGrantAdminScopes: false } }).as('connections');
    openConnections();
    cy.intercept('POST', path, { statusCode: 500 }).as('create');
    cy.get('#agent-name').type('Hermes');
    cy.contains('button', 'Create access token').click();
    cy.wait('@create');
    cy.contains('Could not create the connection').should('be.visible');
    cy.intercept('DELETE', `${path}/${connection.id}`, { statusCode: 500 }).as('revoke');
    cy.get('button[aria-label="Revoke Codex"]').scrollIntoView().click();
    cy.wait('@revoke');
    cy.contains('The token may still be active').should('be.visible');
    cy.get('ul[aria-label="Your agent connections"]').should('contain.text', 'Active');
    cy.intercept('DELETE', `${path}/${connection.id}`, { statusCode: 204 }).as('revoke');
    cy.get('button[aria-label="Revoke Codex"]').click();
    cy.wait('@revoke');
    cy.contains('Codex revoked.').should('be.visible');
    cy.get('button[aria-label="Revoke Codex"]').should('not.exist');
    cy.get('ul[aria-label="Your agent connections"]').should('contain.text', 'Revoked');
  });

  it('displays expired and revoked tokens and fits a narrow screen', () => {
    cy.viewport(390, 844);
    cy.intercept('GET', path, { body: { tokens: [
      { ...connection, expiresAt: '2000-01-01T00:00:00Z' },
      { ...connection, id: 'other', name: 'Hermes', revokedAt: '2026-09-22T00:00:00Z' },
    ], canGrantAdminScopes: true } }).as('connections');
    openConnections('Admin');
    cy.get('ul[aria-label="Your agent connections"]').scrollIntoView().should('contain.text', 'Expired').and('contain.text', 'Revoked');
    cy.get('[role="dialog"]').then(($dialog) => {
      expect($dialog[0].scrollWidth).to.be.at.most($dialog[0].clientWidth);
    });
    cy.get('button[aria-label="Revoke Hermes"]').should('not.exist');
    cy.get('#agent-name').scrollIntoView();
    cy.screenshot('agent-connections-mobile', { capture: 'viewport' });
  });
});
