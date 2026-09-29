import type { AgentScope } from '../../api/agentConnectionsApi';

export const permissions: { scope: AgentScope; label: string; description: string }[] = [
  { scope: 'app:read', label: 'View app information', description: 'Read tournaments, fixtures, standings and permitted predictions.' },
  { scope: 'predictions:write', label: 'Save my predictions', description: 'Create or update your predictions before the deadline.' },
  { scope: 'admin:read', label: 'View admin information', description: 'Read user, prediction and football-provider administration data.' },
  { scope: 'admin:write', label: 'Manage the app', description: 'Create, change or delete tournaments, games, results, users and predictions, and run football imports or syncs.' },
];
