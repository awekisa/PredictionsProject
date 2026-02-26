import './commands';

// Prevent Axios network errors from unintercepted API calls failing tests
Cypress.on('uncaught:exception', () => false);

// Clear localStorage before every test to avoid auth state leaking between tests
beforeEach(() => {
  cy.clearLocalStorage();
});
