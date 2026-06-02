/// <reference types="cypress" />

function buildFakeJwt(role: 'User' | 'Admin'): string {
  const exp = Math.floor(Date.now() / 1000) + 3600;
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(
    JSON.stringify({
      sub: 'test-user-id',
      email: role === 'Admin' ? 'admin@test.com' : 'user@test.com',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role,
      exp,
    })
  );
  return `${header}.${body}.fakesig`;
}

function visitAccountPage(role: 'User' | 'Admin' = 'Admin') {
  cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: [] }).as('tournaments');
  cy.visitAuthenticated('/', role);
  cy.wait('@tournaments');
}

describe('Account drawer', () => {
  it('opens from the navbar user button with profile and security sections', () => {
    visitAccountPage('Admin');

    cy.contains('button', /admin user/i).click();

    cy.get('[role="dialog"][aria-modal="true"]').should('be.visible');
    cy.contains(/profile/i).should('be.visible');
    cy.contains(/security/i).should('be.visible');
    cy.contains('label', /username/i).should('be.visible');
    cy.contains('label', /current password/i).should('be.visible');
    cy.contains('label', /^new password$/i).should('be.visible');
    cy.contains('label', /confirm new password/i).should('be.visible');
  });

  it('saves username and refreshes the visible navbar name', () => {
    visitAccountPage('Admin');
    cy.intercept('PUT', '**/api/auth/me/username', {
      statusCode: 200,
      body: {
        token: buildFakeJwt('Admin'),
        email: 'admin@test.com',
        displayName: 'Dimitar',
      },
    }).as('updateUsername');

    cy.contains('button', /admin user/i).click();
    cy.contains('label', /username/i).parent().find('input').clear().type('Dimitar');
    cy.contains('button', /save username/i).click();

    cy.wait('@updateUsername').its('request.body').should('deep.equal', { displayName: 'Dimitar' });
    cy.contains('button', /dimitar/i).should('be.visible');
    cy.contains(/saved/i).should('be.visible');
  });

  it('validates password rules and confirmation before submit', () => {
    visitAccountPage('Admin');
    cy.contains('button', /admin user/i).click();

    cy.contains('label', /^new password$/i).parent().find('input').type('abc');

    cy.contains('li', /at least 6 characters/i).should('have.attr', 'data-ok', 'false');
    cy.contains('li', /one uppercase letter/i).should('have.attr', 'data-ok', 'false');
    cy.contains('li', /one lowercase letter/i).should('have.attr', 'data-ok', 'true');
    cy.contains('li', /one number/i).should('have.attr', 'data-ok', 'false');
    cy.contains('button', /update password/i).should('be.disabled');

    cy.contains('label', /^new password$/i).parent().find('input').clear().type('Better123');
    cy.contains('label', /confirm new password/i).parent().find('input').type('Different123');

    cy.contains(/passwords do not match/i).should('be.visible');
    cy.contains('button', /update password/i).should('be.disabled');
  });

  it('submits valid password changes and clears the fields', () => {
    visitAccountPage('Admin');
    cy.intercept('POST', '**/api/auth/me/password', { statusCode: 204, body: '' }).as('changePassword');

    cy.contains('button', /admin user/i).click();
    cy.contains('label', /current password/i).parent().find('input').type('Admin123');
    cy.contains('label', /^new password$/i).parent().find('input').type('Better123');
    cy.contains('label', /confirm new password/i).parent().find('input').type('Better123');
    cy.contains('button', /update password/i).click();

    cy.wait('@changePassword').its('request.body').should('deep.equal', {
      currentPassword: 'Admin123',
      newPassword: 'Better123',
    });
    cy.contains(/password changed/i).should('be.visible');
    cy.contains('label', /current password/i).parent().find('input').should('have.value', '');
    cy.contains('label', /^new password$/i).parent().find('input').should('have.value', '');
    cy.contains('label', /confirm new password/i).parent().find('input').should('have.value', '');
  });

  it('shows API error for incorrect current password', () => {
    visitAccountPage('Admin');
    cy.intercept('POST', '**/api/auth/me/password', {
      statusCode: 400,
      body: 'Current password is incorrect.',
    }).as('changePasswordFail');

    cy.contains('button', /admin user/i).click();
    cy.contains('label', /current password/i).parent().find('input').type('Wrong123');
    cy.contains('label', /^new password$/i).parent().find('input').type('Better123');
    cy.contains('label', /confirm new password/i).parent().find('input').type('Better123');
    cy.contains('button', /update password/i).click();

    cy.wait('@changePasswordFail');
    cy.contains(/current password is incorrect/i).should('be.visible');
  });

  it('uses a full-width mobile drawer and closes with Escape', () => {
    cy.viewport(390, 844);
    visitAccountPage('Admin');

    cy.get('button[aria-haspopup="dialog"]').click();
    cy.get('[role="dialog"][aria-modal="true"]')
      .should('be.visible')
      .then(($drawer) => {
        expect($drawer.outerWidth()).to.be.greaterThan(360);
      });
    cy.contains('label', /username/i).should('be.visible');
    cy.contains('label', /current password/i).should('be.visible');

    cy.get('body').type('{esc}');
    cy.get('[role="dialog"][aria-modal="true"]').should('not.exist');
  });
});
