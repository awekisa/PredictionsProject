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

// Visits a URL with auth pre-set in localStorage BEFORE React mounts,
// so AuthProvider reads the token correctly on the initial render.
Cypress.Commands.add('visitAuthenticated', (url: string, role: 'User' | 'Admin' = 'User') => {
  const token = buildFakeJwt(role);
  const email = role === 'Admin' ? 'admin@test.com' : 'user@test.com';
  const displayName = role === 'Admin' ? 'Admin User' : 'Test User';

  cy.visit(url, {
    onBeforeLoad(win) {
      win.localStorage.setItem('token', token);
      win.localStorage.setItem('userEmail', email);
      win.localStorage.setItem('userDisplayName', displayName);
    },
  });
});

declare global {
  namespace Cypress {
    interface Chainable {
      visitAuthenticated(url: string, role?: 'User' | 'Admin'): Chainable<void>;
    }
  }
}
