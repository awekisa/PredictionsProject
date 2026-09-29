import { useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { login } from '../../api/authApi';
import apiClient from '../../api/apiClient';
import type { AgentScope } from '../../api/agentConnectionsApi';
import { permissions } from './agentPermissions';
import styles from './AccountDrawer.module.css';
import page from './OAuthConsentPage.module.css';

interface Consent { clientName: string; clientId: string; redirectUri: string; scopes: AgentScope[] }

export default function OAuthConsentPage() {
  const [params] = useSearchParams();
  const request = params.get('request') ?? '';
  const { isAuthenticated, user, handleAuthResponse, logout } = useAuth();
  const [consent, setConsent] = useState<Consent | null>(null);
  const [scopes, setScopes] = useState<AgentScope[]>([]);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const path = `/auth/mcp-oauth/consent/${encodeURIComponent(request)}`;

  useEffect(() => {
    if (!isAuthenticated || !/^[A-Za-z0-9_-]{43}$/.test(request)) return;
    let active = true;
    apiClient.get<Consent>(path).then(({ data }) => {
      if (active) { setConsent(data); setScopes(data.scopes.filter(s => s === 'app:read')); setError(''); }
    }).catch(() => { if (active) setError('This connection request is invalid or has expired. Start again from your connector.'); });
    return () => { active = false; };
  }, [isAuthenticated, request, path]);

  async function signIn(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('');
    try { handleAuthResponse(await login({ email, password })); setPassword(''); }
    catch { setError('Could not sign in. Check your email and password.'); }
    finally { setBusy(false); }
  }
  async function decide(approve: boolean) {
    setBusy(true); setError('');
    try {
      const { data } = await apiClient.post<{ redirectUrl: string }>(path, { approve, scopes });
      window.location.assign(data.redirectUrl);
    } catch (e) {
      if ((e as { response?: { status: number } }).response?.status === 401) { logout(); setConsent(null); }
      setError('Could not complete the connection. Your session, request or permissions may have changed. Sign in or start again from your connector.');
      setBusy(false);
    }
  }
  return <main className={page.container}><section className={page.card}>
    <h1>Connect to Predictions</h1>
    {!/^[A-Za-z0-9_-]{43}$/.test(request) ? <p role="alert">Invalid connection request. Start again from your connector.</p> : <>
      {error && <p role="alert" className={styles.formError}>{error}</p>}
      {!isAuthenticated ? <>
        <p>Sign in with your Predictions account to review this connection.</p>
        <form className={styles.form} onSubmit={signIn}>
          <div className={styles.field}><label htmlFor="oauth-email">Email</label><input id="oauth-email" type="email" autoComplete="username" value={email} onChange={e => setEmail(e.target.value)} required /></div>
          <div className={styles.field}><label htmlFor="oauth-password">Password</label><input id="oauth-password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required /></div>
          <button className={styles.primaryBtn} disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
        </form>
      </> : consent ? <>
        <p><strong>{consent.clientName}</strong> wants to connect to your Predictions account.</p>
        <p>Signed in as <strong>{user?.email}</strong>.</p>
        <p>After approval, return to <strong>{new URL(consent.redirectUri).hostname}</strong>.</p>
        <p className={styles.hint}>Only approve a connection you started. Client names are supplied by the connecting app.</p>
        <details><summary>Connection details</summary><p className={page.identifier}>Client: {consent.clientId}</p><p className={page.identifier}>Return address: {consent.redirectUri}</p></details>
        <fieldset className={styles.permissions} disabled={busy}><legend>Choose permissions</legend>
          {permissions.filter(p => consent.scopes.includes(p.scope)).map(p => <label key={p.scope} className={styles.permission}>
            <input type="checkbox" value={p.scope} checked={scopes.includes(p.scope)} onChange={e => setScopes(current => e.target.checked ? [...current, p.scope] : current.filter(s => s !== p.scope))} />
            <span><strong>{p.label}</strong><span className={styles.permissionDescription}>{p.description}</span></span>
          </label>)}
        </fieldset>
        <p className={styles.hint}>You can revoke this connection in Account settings → Agent connections.</p>
        <div className={page.actions}><button className={styles.primaryBtn} disabled={busy || scopes.length === 0} onClick={() => decide(true)}>Allow connection</button>
          <button disabled={busy} onClick={() => decide(false)}>Cancel</button></div>
      </> : !error && <p role="status">Loading connection…</p>}
    </>}
  </section></main>;
}
