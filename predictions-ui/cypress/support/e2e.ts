import './commands';

// Prevent unhandled Axios network errors (unintercepted API calls) from failing tests
Cypress.on('uncaught:exception', () => false);

// Catch-all intercepts so any API call not specifically mocked returns an empty response
// instead of hitting localhost:5039 which doesn't exist in CI.
// Specific intercepts registered in each test take priority (Cypress uses last-match-wins).
beforeEach(() => {
  cy.intercept('GET', '**/api/**', { statusCode: 200, body: [] });
  cy.intercept('POST', '**/api/**', { statusCode: 200, body: {} });
  cy.intercept('PUT', '**/api/**', { statusCode: 200, body: {} });
  cy.intercept('DELETE', '**/api/**', { statusCode: 204 });
});
