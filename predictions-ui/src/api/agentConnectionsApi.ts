import apiClient from './apiClient';

export type AgentScope = 'app:read' | 'predictions:write' | 'admin:read' | 'admin:write';

export interface AgentConnection {
  id: string;
  name: string;
  scopes: AgentScope[];
  createdAt: string;
  expiresAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

interface ConnectionList {
  tokens: AgentConnection[];
  canGrantAdminScopes: boolean;
}

const path = '/auth/me/agent-connections';
export const listAgentConnections = () => apiClient.get<ConnectionList>(path).then((response) => response.data);
export const createAgentConnection = (data: { name: string; expirationDays: number; scopes: AgentScope[] }) =>
  apiClient.post<{ token: string; connection: AgentConnection }>(path, data).then((response) => response.data);
export const revokeAgentConnection = (id: string) => apiClient.delete<void>(`${path}/${id}`);
