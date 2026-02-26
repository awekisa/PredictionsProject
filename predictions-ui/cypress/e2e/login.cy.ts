describe('Login page', () => {
  beforeEach(() => {
    cy.visit('/login');
  });

  it('shows login form', () => {
    cy.get('input[type="email"]').should('exist');
    cy.get('input[type="password"]').should('exist');
    cy.contains('button', /sign in/i).should('exist');
  });

  it('shows error on invalid credentials', () => {
    cy.intercept('POST', '**/api/auth/login', {
      statusCode: 401,
      body: 'Invalid credentials',
    }).as('loginFail');

    cy.get('input[type="email"]').type('bad@test.com');
    cy.get('input[type="password"]').type('wrongpassword');
    cy.contains('button', /sign in/i).click();

    cy.wait('@loginFail');
    // App shows "Login failed. Please check your credentials." or the server message
    cy.contains(/login failed|invalid/i).should('exist');
  });

  it('redirects away from /login on successful login', () => {
    cy.intercept('POST', '**/api/auth/login', {
      statusCode: 200,
      body: {
        token: buildFakeJwt('User'),
        email: 'user@test.com',
        displayName: 'Test User',
      },
    }).as('loginOk');

    cy.intercept('GET', '**/api/tournaments', { statusCode: 200, body: [] });

    cy.get('input[type="email"]').type('user@test.com');
    cy.get('input[type="password"]').type('Password123!');
    cy.contains('button', /sign in/i).click();

    cy.wait('@loginOk');
    cy.url().should('not.include', '/login');
  });
});

function buildFakeJwt(role: string): string {
  const exp = Math.floor(Date.now() / 1000) + 3600;
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(
    JSON.stringify({
      sub: 'uid1',
      email: 'user@test.com',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': role,
      exp,
    })
  );
  return `${header}.${body}.fakesig`;
}
