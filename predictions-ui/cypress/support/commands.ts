/// <reference types="cypress" />

// Build a minimal valid JWT with the given payload (no real signing — tests only)
function buildFakeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fakesig`;
}

Cypress.Commands.add('loginAs', (role: 'User' | 'Admin' = 'User') => {
  const exp = Math.floor(Date.now() / 1000) + 3600; // 1 hour from now
  const token = buildFakeJwt({
    sub: 'test-user-id',
    email: role === 'Admin' ? 'admin@test.com' : 'user@test.com',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role,
    exp,
  });

  const email = role === 'Admin' ? 'admin@test.com' : 'user@test.com';
  const displayName = role === 'Admin' ? 'Admin User' : 'Test User';

  cy.window().then((win) => {
    win.localStorage.setItem('token', token);
    win.localStorage.setItem('userEmail', email);
    win.localStorage.setItem('userDisplayName', displayName);
  });
});

declare global {
  namespace Cypress {
    interface Chainable {
      loginAs(role?: 'User' | 'Admin'): Chainable<void>;
    }
  }
}
