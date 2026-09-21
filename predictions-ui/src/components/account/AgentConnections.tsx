import { useEffect, useState, type FormEvent } from 'react';
import {
  createAgentConnection, listAgentConnections, revokeAgentConnection,
  type AgentConnection, type AgentScope,
} from '../../api/agentConnectionsApi';
import styles from './AccountDrawer.module.css';

const permissions: { scope: AgentScope; label: string; description: string }[] = [
  { scope: 'app:read', label: 'View app information', description: 'Read tournaments, fixtures, standings and permitted predictions.' },
  { scope: 'predictions:write', label: 'Save my predictions', description: 'Create or update your predictions before the deadline.' },
  { scope: 'admin:read', label: 'View admin information', description: 'Read user, prediction and football-provider administration data.' },
  { scope: 'admin:write', label: 'Manage the app', description: 'Create, change or delete tournaments, games, results, users and predictions, and run football imports or syncs.' },
];

function statusOf(connection: AgentConnection): string {
  if (connection.revokedAt) return 'Revoked';
  return Date.parse(connection.expiresAt) <= Date.now() ? 'Expired' : 'Active';
}

function dateLabel(value: string): string {
  return new Date(value).toLocaleString();
}

export default function AgentConnections() {
  const [expanded, setExpanded] = useState(false);
  return (
    <section className={styles.section} aria-labelledby="agent-connections-heading">
      <h3 id="agent-connections-heading" className={styles.secHead}>Agent connections</h3>
      <details onToggle={(event) => setExpanded(event.currentTarget.open)}>
        <summary className={styles.connectionSummary}>Manage agent connections</summary>
        {expanded && <ConnectionManager />}
      </details>
    </section>
  );
}

function ConnectionManager() {
  const [connections, setConnections] = useState<AgentConnection[]>([]);
  const [canGrantAdmin, setCanGrantAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [name, setName] = useState('');
  const [expirationDays, setExpirationDays] = useState(30);
  const [scopes, setScopes] = useState<AgentScope[]>(['app:read']);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState('');
  const [revealed, setRevealed] = useState<{ token: string; id: string } | null>(null);
  const [revoking, setRevoking] = useState<string | null>(null);
  const [revokeError, setRevokeError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    let active = true;
    listAgentConnections().then((result) => {
      if (!active) return;
      setConnections(result.tokens);
      setCanGrantAdmin(result.canGrantAdminScopes);
      setLoadError('');
    }).catch(() => {
      if (active) setLoadError('Could not load agent connections. Try again.');
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reloadKey]);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setCreating(true);
    setCreateError('');
    setNotice('');
    try {
      const result = await createAgentConnection({ name: name.trim(), expirationDays, scopes });
      setConnections((current) => [result.connection, ...current]);
      // Only held in this mounted component; never persisted or passed to analytics/logging.
      setRevealed({ token: result.token, id: result.connection.id });
      setName('');
    } catch (error) {
      const status = (error as { response?: { status?: number } }).response?.status;
      setCreateError(status === 403
        ? 'Admin permissions are no longer available for this account. Close and reopen this section to refresh.'
        : 'Could not create the connection. Check the name and permissions and try again.');
    } finally { setCreating(false); }
  };

  const revoke = async (connection: AgentConnection) => {
    setRevoking(connection.id);
    setRevokeError('');
    setNotice('');
    try {
      await revokeAgentConnection(connection.id);
      setConnections((current) => current.map((item) => item.id === connection.id
        ? { ...item, revokedAt: new Date().toISOString() } : item));
      if (revealed?.id === connection.id) setRevealed(null);
      setNotice(`${connection.name} revoked. Its token can no longer be used.`);
    } catch {
      setRevokeError(`Could not revoke ${connection.name}. The token may still be active. Try again.`);
    } finally { setRevoking(null); }
  };

  if (loading) return <p role="status" className={styles.hint}>Loading agent connections…</p>;
  if (loadError) return <div className={styles.connectionContent}>
    <p role="alert" className={styles.formError}>{loadError}</p>
    <button type="button" onClick={() => { setLoading(true); setReloadKey((value) => value + 1); }}>Retry loading</button>
  </div>;

  return <div className={styles.connectionContent}>
    <p className={styles.hint}>Create a separate access token for each agent, such as Codex or Hermes. Each token only allows the permissions you select, within your account’s access.</p>
    <form className={styles.form} onSubmit={create}>
      <div className={styles.field}>
        <label htmlFor="agent-name">Connection name</label>
        <input id="agent-name" value={name} onChange={(event) => setName(event.target.value)} maxLength={100} required placeholder="e.g. Codex" autoComplete="off" />
      </div>
      <div className={styles.field}>
        <label htmlFor="agent-expiry">Expires after</label>
        <select id="agent-expiry" value={expirationDays} onChange={(event) => setExpirationDays(Number(event.target.value))}>
          {[7, 30, 90].map((days) => <option key={days} value={days}>{days} days</option>)}
        </select>
      </div>
      <fieldset className={styles.permissions}>
        <legend>Permissions</legend>
        {permissions.filter((item) => canGrantAdmin || !item.scope.startsWith('admin:')).map((item) =>
          <label key={item.scope} className={styles.permission}>
            <input type="checkbox" checked={scopes.includes(item.scope)} value={item.scope}
              onChange={(event) => setScopes((current) => event.target.checked
                ? [...current, item.scope] : current.filter((scope) => scope !== item.scope))} />
            <span><strong>{item.label}</strong><span className={styles.permissionDescription}>{item.description}</span></span>
          </label>)}
      </fieldset>
      {createError && <p role="alert" className={styles.formError}>{createError}</p>}
      <button type="submit" className={styles.primaryBtn} disabled={creating || !name.trim() || scopes.length === 0 || !!revealed}>
        {creating ? 'Creating…' : 'Create access token'}
      </button>
    </form>
    {revealed && <div className={styles.tokenReveal} role="region" aria-label="New access token">
      <strong>Save this token now</strong>
      <p className={styles.hint}>It is shown only once. Store it in your agent’s secret settings. Closing this section or account settings hides it permanently.</p>
      <label htmlFor="agent-new-token">Access token</label>
      <textarea id="agent-new-token" readOnly value={revealed.token} autoComplete="off" spellCheck={false} rows={3} onFocus={(event) => event.currentTarget.select()} />
      <button type="button" onClick={() => setRevealed(null)}>I’ve saved it — hide token</button>
    </div>}
    {notice && <p role="status" className={styles.hint}>{notice}</p>}
    {revokeError && <p role="alert" className={styles.formError}>{revokeError}</p>}
    {connections.length === 0 ? <p className={styles.hint}>No agent connections yet.</p> :
      <ul className={styles.connectionList} aria-label="Your agent connections">
        {connections.map((connection) => <li key={connection.id} className={styles.connectionCard}>
          <div className={styles.connectionTitle}><strong>{connection.name}</strong><span>{statusOf(connection)}</span></div>
          <dl className={styles.connectionMeta}>
            <dt>Permissions</dt><dd>{connection.scopes.map((scope) => permissions.find((item) => item.scope === scope)?.label ?? scope).join(', ')}</dd>
            <dt>Created</dt><dd>{dateLabel(connection.createdAt)}</dd>
            <dt>Expires</dt><dd>{dateLabel(connection.expiresAt)}</dd>
            <dt>Last used</dt><dd>{connection.lastUsedAt ? dateLabel(connection.lastUsedAt) : 'Never'}</dd>
            {connection.revokedAt && <><dt>Revoked</dt><dd>{dateLabel(connection.revokedAt)}</dd></>}
          </dl>
          {!connection.revokedAt && <button type="button" disabled={revoking !== null} aria-label={`Revoke ${connection.name}`} onClick={() => revoke(connection)}>
            {revoking === connection.id ? 'Revoking…' : 'Revoke token'}
          </button>}
        </li>)}
      </ul>}
  </div>;
}
