import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { changePassword, updateUsername } from '../../api/authApi';
import { useAuth } from '../../hooks/useAuth';
import styles from './AccountDrawer.module.css';

interface Props {
  open: boolean;
  onClose: () => void;
}

function getInitials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => word[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

function passwordChecks(password: string) {
  return [
    { key: 'len', label: 'At least 6 characters', ok: password.length >= 6 },
    { key: 'upper', label: 'One uppercase letter', ok: /[A-Z]/.test(password) },
    { key: 'lower', label: 'One lowercase letter', ok: /[a-z]/.test(password) },
    { key: 'digit', label: 'One number', ok: /\d/.test(password) },
  ];
}

function passwordStrength(password: string) {
  if (!password) return { score: 0, label: '', color: 'var(--color-border)' };

  let score = passwordChecks(password).filter((check) => check.ok).length;
  if (password.length >= 12 && score === 4) score = 5;

  const labels: Record<number, { label: string; color: string }> = {
    1: { label: 'Very weak', color: 'var(--color-danger)' },
    2: { label: 'Weak', color: 'var(--color-warning)' },
    3: { label: 'Fair', color: 'var(--color-warning)' },
    4: { label: 'Good', color: 'var(--color-success)' },
    5: { label: 'Strong', color: 'var(--color-success)' },
  };

  return { score, ...(labels[score] ?? labels[1]) };
}

function getErrorMessage(error: unknown, fallback: string): string {
  const data = (error as { response?: { data?: unknown } }).response?.data;
  return typeof data === 'string' && data.trim() ? data : fallback;
}

export default function AccountDrawer({ open, onClose }: Props) {
  const { user, handleAuthResponse } = useAuth();

  useEffect(() => {
    if (!open) return undefined;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };

    const previousOverflow = document.body.style.overflow;
    document.addEventListener('keydown', onKeyDown);
    document.body.style.overflow = 'hidden';

    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [open, onClose]);

  if (!open || !user) return null;

  const titleId = 'account-drawer-title';

  return (
    <>
      <div className={styles.overlay} onMouseDown={onClose} aria-hidden="true" />
      <aside className={styles.drawer} role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <header className={styles.head}>
          <div className={styles.avatar} aria-hidden="true">
            {getInitials(user.displayName)}
          </div>
          <div className={styles.headMeta}>
            <h2 id={titleId}>Account settings</h2>
            <span className={styles.name}>{user.displayName}</span>
            <span className={styles.sub}>{user.email}</span>
          </div>
          <button className={styles.closeX} onClick={onClose} aria-label="Close account settings">
            ×
          </button>
        </header>

        <div className={styles.body}>
          <section className={styles.section}>
            <div className={styles.secHead}>Profile</div>
            <UsernameForm current={user.displayName} onSaved={handleAuthResponse} />
          </section>

          <section className={styles.section}>
            <div className={styles.secHead}>Security</div>
            <PasswordForm />
          </section>
        </div>
      </aside>
    </>
  );
}

function UsernameForm({
  current,
  onSaved,
}: {
  current: string;
  onSaved: (response: Awaited<ReturnType<typeof updateUsername>>) => void;
}) {
  const [name, setName] = useState(current);
  const [touched, setTouched] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    setName(current);
  }, [current]);

  const trimmed = name.trim();
  const tooShort = touched && trimmed.length < 2;
  const changed = trimmed !== current && trimmed.length >= 2;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setTouched(true);
    setError('');
    setSaved(false);

    if (trimmed.length < 2) return;

    setSaving(true);
    try {
      const response = await updateUsername({ displayName: trimmed });
      onSaved(response);
      setName(response.displayName);
      setSaved(true);
      window.setTimeout(() => setSaved(false), 2500);
    } catch (submitError) {
      setError(getErrorMessage(submitError, 'Could not update username.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className={styles.form} onSubmit={submit}>
      {error && <div className={styles.formError}>{error}</div>}
      <div className={styles.field}>
        <label htmlFor="account-display-name">Username</label>
        <input
          id="account-display-name"
          type="text"
          value={name}
          data-invalid={tooShort}
          onChange={(event) => {
            setName(event.target.value);
            setTouched(true);
          }}
        />
        {tooShort ? (
          <div className={styles.fieldError}>Username must be at least 2 characters.</div>
        ) : (
          <div className={styles.hint}>This is the name shown across Predictions.</div>
        )}
      </div>
      <div className={styles.actions}>
        {saved && <span className={styles.okMsg}>✓ Saved</span>}
        <button className={styles.primaryBtn} type="submit" disabled={!changed || saving}>
          {saving ? 'Saving…' : 'Save username'}
        </button>
      </div>
    </form>
  );
}

function PasswordForm() {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [revealed, setRevealed] = useState(false);
  const [touched, setTouched] = useState<{ confirm?: boolean; next?: boolean }>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  const checks = useMemo(() => passwordChecks(newPassword), [newPassword]);
  const strength = useMemo(() => passwordStrength(newPassword), [newPassword]);
  const allRulesPass = checks.every((check) => check.ok);
  const passwordsMatch = newPassword.length > 0 && newPassword === confirmPassword;
  const sameAsCurrent = newPassword.length > 0 && newPassword === currentPassword;
  const confirmError = Boolean(touched.confirm && confirmPassword && !passwordsMatch);
  const ready = currentPassword.length > 0 && allRulesPass && passwordsMatch && !sameAsCurrent;
  const inputType = revealed ? 'text' : 'password';
  const revealLabel = revealed ? 'Hide passwords' : 'Show passwords';

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setSaved(false);

    if (!ready) return;

    setSaving(true);
    try {
      await changePassword({ currentPassword, newPassword });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setTouched({});
      setSaved(true);
      window.setTimeout(() => setSaved(false), 2500);
    } catch (submitError) {
      setError(getErrorMessage(submitError, 'Current password is incorrect.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className={styles.form} onSubmit={submit}>
      {error && <div className={styles.formError}>{error}</div>}

      <div className={styles.field}>
        <label htmlFor="account-current-password">Current password</label>
        <div className={styles.inputWrap}>
          <input
            id="account-current-password"
            type={inputType}
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
          />
          <button type="button" className={styles.peek} onClick={() => setRevealed((value) => !value)} aria-label={revealLabel}>
            {revealed ? 'Hide' : 'Show'}
          </button>
        </div>
      </div>

      <div className={styles.field}>
        <label htmlFor="account-new-password">New password</label>
        <div className={styles.inputWrap}>
          <input
            id="account-new-password"
            type={inputType}
            value={newPassword}
            data-invalid={Boolean(touched.next && newPassword && !allRulesPass)}
            onChange={(event) => {
              setNewPassword(event.target.value);
              setTouched((value) => ({ ...value, next: true }));
            }}
          />
          <button type="button" className={styles.peek} onClick={() => setRevealed((value) => !value)} aria-label={revealLabel}>
            {revealed ? 'Hide' : 'Show'}
          </button>
        </div>
        {newPassword && (
          <>
            <div className={styles.meter} aria-hidden="true">
              {[0, 1, 2, 3].map((index) => (
                <span
                  key={index}
                  style={{
                    background:
                      index < Math.min(strength.score, 4) ? strength.color : 'var(--color-border)',
                  }}
                />
              ))}
            </div>
            <div className={styles.meterLabel} style={{ color: strength.color }}>
              {strength.label}
            </div>
            <ul className={styles.checklist}>
              {checks.map((check) => (
                <li key={check.key} data-ok={check.ok}>
                  <span className={styles.tick}>{check.ok ? '✓' : '○'}</span>
                  {check.label}
                </li>
              ))}
            </ul>
          </>
        )}
        {sameAsCurrent && <div className={styles.fieldError}>New password must differ from current.</div>}
      </div>

      <div className={styles.field}>
        <label htmlFor="account-confirm-password">Confirm new password</label>
        <div className={styles.inputWrap}>
          <input
            id="account-confirm-password"
            type={inputType}
            value={confirmPassword}
            data-invalid={confirmError}
            onChange={(event) => {
              setConfirmPassword(event.target.value);
              setTouched((value) => ({ ...value, confirm: true }));
            }}
          />
          <button type="button" className={styles.peek} onClick={() => setRevealed((value) => !value)} aria-label={revealLabel}>
            {revealed ? 'Hide' : 'Show'}
          </button>
        </div>
        {confirmError && <div className={styles.fieldError}>Passwords do not match.</div>}
      </div>

      <div className={styles.actions}>
        {saved && <span className={styles.okMsg}>✓ Password changed</span>}
        <button className={styles.primaryBtn} type="submit" disabled={!ready || saving}>
          {saving ? 'Updating…' : 'Update password'}
        </button>
      </div>
    </form>
  );
}
