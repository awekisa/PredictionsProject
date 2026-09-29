/// <reference types="cypress" />
const requestId = 'a'.repeat(43);
const consentPath = `**/api/auth/mcp-oauth/consent/${requestId}`;
const consentUrl = `/oauth/consent?request=${requestId}`;
const consent = { clientName: 'Claude', clientId: 'registered-client', redirectUri: 'https://claude.ai/api/mcp/auth_callback', scopes: ['app:read', 'predictions:write'] };

describe('OAuth consent', () => {
  it('signs in on the consent page and submits only the selected permissions', () => {
    const token = `${btoa('{}')}.${btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }))}.test`;
    cy.intercept('POST', '**/api/auth/login', { body: { token, email: 'user@test.com', displayName: 'User' } }).as('login');
    cy.intercept('GET', consentPath, { body: consent }).as('consent');
    cy.intercept('POST', consentPath, { body: { redirectUrl: '/oauth/consent?finished=1' } }).as('decision');
    cy.visit(consentUrl);
    cy.get('#oauth-email').type('user@test.com');
    cy.get('#oauth-password').type('TestPass123!');
    cy.contains('button', 'Sign in').click();
    cy.wait('@login'); cy.wait('@consent');
    cy.contains('Claude').should('be.visible');
    cy.get('input[value="app:read"]').should('be.checked');
    cy.get('input[value="predictions:write"]').should('not.be.checked').check();
    cy.get('input[value="admin:read"]').should('not.exist');
    cy.contains('button', 'Allow connection').click();
    cy.wait('@decision').its('request.body').should('deep.equal', { approve: true, scopes: ['app:read', 'predictions:write'] });
    cy.location('search').should('equal', '?finished=1');
  });
  it('shows live admin permissions, allows denial and fits a narrow screen', () => {
    cy.viewport(390, 844);
    cy.intercept('GET', consentPath, { body: { ...consent, scopes: [...consent.scopes, 'admin:read', 'admin:write'] } }).as('consent');
    cy.intercept('POST', consentPath, { body: { redirectUrl: '/oauth/consent?cancelled=1' } }).as('decision');
    cy.visitAuthenticated(consentUrl, 'Admin'); cy.wait('@consent');
    cy.get('input[value="admin:write"]').should('not.be.checked');
    cy.get('main').then($main => expect($main[0].scrollWidth).to.be.at.most($main[0].clientWidth));
    cy.screenshot('oauth-consent-mobile', { capture: 'fullPage' });
    cy.contains('button', 'Cancel').click();
    cy.wait('@decision').its('request.body.approve').should('be.false');
    cy.location('search').should('equal', '?cancelled=1');
  });
  it('does not grant admin based on a stale browser role and reports expired requests', () => {
    cy.intercept('GET', consentPath, { body: consent }).as('consent');
    cy.visitAuthenticated(consentUrl, 'Admin'); cy.wait('@consent');
    cy.get('input[value="admin:read"]').should('not.exist');
    cy.get('input[value="app:read"]').uncheck();
    cy.contains('button', 'Allow connection').should('be.disabled');
    cy.intercept('GET', consentPath, { statusCode: 400 });
    cy.reload(); cy.contains('invalid or has expired').should('be.visible');
    cy.contains('button', 'Allow connection').should('not.exist');
  });
  it('lists and revokes OAuth connections alongside manual tokens', () => {
    const connection = { id: 'connection', name: 'Claude', isOAuth: true, scopes: ['app:read'], createdAt: '2026-09-29T08:00:00Z', expiresAt: '2099-01-01T00:00:00Z', lastUsedAt: '2026-09-29T08:01:00Z', revokedAt: null };
    cy.intercept('GET', '**/api/tournaments', { body: [] });
    cy.intercept('GET', '**/api/auth/me/agent-connections', { body: { tokens: [connection], canGrantAdminScopes: false } }).as('connections');
    cy.intercept('DELETE', '**/api/auth/me/agent-connections/connection', { statusCode: 204 }).as('revoke');
    cy.visitAuthenticated('/'); cy.get('button[aria-haspopup="dialog"]').click();
    cy.contains('summary', 'Manage agent connections').scrollIntoView().click(); cy.wait('@connections');
    cy.contains('OAuth sign-in').scrollIntoView().should('be.visible');
    cy.get('button[aria-label="Revoke Claude"]').scrollIntoView().click(); cy.wait('@revoke');
    cy.get('ul[aria-label="Your agent connections"]').should('contain.text', 'Revoked');
  });
});
