describe('Login page', () => {
  beforeEach(() => {
    cy.visit('/login');
  });

  it('shows login form', () => {
    cy.get('input[type="email"], input[placeholder*="email" i], input[name="email"]')
      .should('exist');
    cy.get('input[type="password"]').should('exist');
    cy.contains('button', /log in|sign in/i).should('exist');
  });

  it('shows error on invalid credentials', () => {
    cy.intercept('POST', '**/api/auth/login', {
      statusCode: 401,
      body: { message: 'Invalid credentials' },
    }).as('loginFail');

    cy.get('input[type="email"], input[name="email"]').type('bad@test.com');
    cy.get('input[type="password"]').type('wrongpassword');
    cy.contains('button', /log in|sign in/i).click();

    cy.wait('@loginFail');
    cy.contains(/invalid|incorrect|wrong|failed/i).should('exist');
  });

  it('redirects to tournaments on successful login', () => {
    cy.intercept('POST', '**/api/auth/login', {
      statusCode: 200,
      body: {
        token: buildFakeJwt('User'),
        email: 'user@test.com',
        displayName: 'Test User',
      },
    }).as('loginOk');

    cy.get('input[type="email"], input[name="email"]').type('user@test.com');
    cy.get('input[type="password"]').type('Password123!');
    cy.contains('button', /log in|sign in/i).click();

    cy.wait('@loginOk');
    cy.url().should('include', '/tournaments');
  });
});

function buildFakeJwt(role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const exp = Math.floor(Date.now() / 1000) + 3600;
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
