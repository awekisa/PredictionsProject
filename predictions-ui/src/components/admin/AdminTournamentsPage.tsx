import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import * as adminTournamentApi from '../../api/adminTournamentApi';
import * as footballApi from '../../api/footballApi';
import type { LeagueSearchResult, TournamentResponse } from '../../types';
import ConfirmDialog from '../common/ConfirmDialog';
import { formatDate } from '../../utils/formatDate';
import styles from './AdminTournamentsPage.module.css';

export default function AdminTournamentsPage() {
  const [tournaments, setTournaments] = useState<TournamentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);

  // Import modal state
  const [showImport, setShowImport] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<LeagueSearchResult[]>([]);
  const [searching, setSearching] = useState(false);
  const [selectedLeague, setSelectedLeague] = useState<LeagueSearchResult | null>(null);
  const [importSeason, setImportSeason] = useState<number | ''>('');
  const [importName, setImportName] = useState('');
  const [importing, setImporting] = useState(false);

  // Sync state: tournamentId → status message
  const [syncMessages, setSyncMessages] = useState<Record<number, string>>({});

  const searchDebounce = useRef<ReturnType<typeof setTimeout> | null>(null);
  const navigate = useNavigate();

  const load = () => {
    setLoading(true);
    adminTournamentApi.getAll().then(setTournaments).finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    setEditingId(null);
    setName('');
    setShowForm(true);
  };

  const openEdit = (t: TournamentResponse) => {
    setEditingId(t.id);
    setName(t.name);
    setShowForm(true);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (editingId) {
      await adminTournamentApi.update(editingId, { name });
    } else {
      await adminTournamentApi.create({ name });
    }
    setShowForm(false);
    load();
  };

  const handleDelete = async (id: number) => {
    await adminTournamentApi.remove(id);
    setPendingDeleteId(null);
    load();
  };

  // ── Import modal ──────────────────────────────────────────────────────────

  const openImport = () => {
    setSearchQuery('');
    setSearchResults([]);
    setSelectedLeague(null);
    setImportSeason('');
    setImportName('');
    setShowImport(true);
  };

  const handleSearchChange = (q: string) => {
    setSearchQuery(q);
    setSelectedLeague(null);
    if (searchDebounce.current) clearTimeout(searchDebounce.current);
    if (!q.trim()) { setSearchResults([]); return; }
    searchDebounce.current = setTimeout(async () => {
      setSearching(true);
      try {
        const results = await footballApi.searchLeagues(q);
        setSearchResults(results);
      } finally {
        setSearching(false);
      }
    }, 400);
  };

  const selectLeague = (league: LeagueSearchResult) => {
    setSelectedLeague(league);
    setImportName(league.name);
    setImportSeason(league.seasons[0] ?? '');
    setSearchResults([]);
    setSearchQuery(league.name);
  };

  const handleImport = async (e: FormEvent) => {
    e.preventDefault();
    if (!selectedLeague || importSeason === '') return;
    setImporting(true);
    try {
      await footballApi.importLeague({
        leagueId: selectedLeague.leagueId,
        season: Number(importSeason),
        name: importName
      });
      setShowImport(false);
      load();
    } finally {
      setImporting(false);
    }
  };

  // ── Sync scores ───────────────────────────────────────────────────────────

  const handleSync = async (id: number) => {
    setSyncMessages(prev => ({ ...prev, [id]: 'Syncing…' }));
    try {
      const { updated } = await footballApi.syncScores(id);
      setSyncMessages(prev => ({ ...prev, [id]: `${updated} game${updated === 1 ? '' : 's'} updated` }));
      if (updated > 0) load();
    } catch {
      setSyncMessages(prev => ({ ...prev, [id]: 'Sync failed' }));
    }
    setTimeout(() => setSyncMessages(prev => { const n = { ...prev }; delete n[id]; return n; }), 4000);
  };

  if (loading) return <div className={styles.loading}>Loading...</div>;

  return (
    <div>
      <div className={styles.header}>
        <h1>Manage Tournaments</h1>
        <div className={styles.headerActions}>
          <button className={styles.importBtn} onClick={openImport}>
            Import from API
          </button>
          <button className={styles.createBtn} onClick={openCreate}>
            + New Tournament
          </button>
        </div>
      </div>

      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {tournaments.map((t) => (
              <tr key={t.id}>
                <td>
                  {t.name}
                  {t.externalLeagueId != null && (
                    <span className={styles.apiTag}>API</span>
                  )}
                </td>
                <td>{formatDate(t.createdAt)}</td>
                <td>
                  <div className={styles.actions}>
                    <button
                      className={styles.manageBtn}
                      onClick={() => navigate(`/admin/tournaments/${t.id}/games`)}
                    >
                      Games
                    </button>
                    <button className={styles.editBtn} onClick={() => openEdit(t)}>
                      Edit
                    </button>
                    {t.externalLeagueId != null && (
                      <button
                        className={styles.syncBtn}
                        onClick={() => handleSync(t.id)}
                        disabled={syncMessages[t.id] === 'Syncing…'}
                      >
                        {syncMessages[t.id] ?? 'Sync Scores'}
                      </button>
                    )}
                    <button className={styles.deleteBtn} onClick={() => setPendingDeleteId(t.id)}>
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {pendingDeleteId !== null && (
        <ConfirmDialog
          message="Delete this tournament? All games and predictions will be removed."
          onConfirm={() => handleDelete(pendingDeleteId)}
          onCancel={() => setPendingDeleteId(null)}
        />
      )}

      {/* ── Create/Edit form ── */}
      {showForm && (
        <div className={styles.formOverlay}>
          <div className={styles.formCard}>
            <h2>{editingId ? 'Edit Tournament' : 'New Tournament'}</h2>
            <form onSubmit={handleSubmit}>
              <div>
                <label>Name</label>
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <div className={styles.formActions}>
                <button
                  type="button"
                  className={styles.cancelBtn}
                  onClick={() => setShowForm(false)}
                >
                  Cancel
                </button>
                <button type="submit" className={styles.saveBtn}>
                  {editingId ? 'Save' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Import modal ── */}
      {showImport && (
        <div className={styles.formOverlay}>
          <div className={styles.formCard}>
            <h2>Import from API-Football</h2>
            <form onSubmit={handleImport}>
              <div className={styles.searchWrap}>
                <label>Search league</label>
                <input
                  value={searchQuery}
                  onChange={(e) => handleSearchChange(e.target.value)}
                  placeholder="e.g. Premier League"
                  autoFocus
                />
                {searching && <div className={styles.searchHint}>Searching…</div>}
                {searchResults.length > 0 && (
                  <ul className={styles.suggestions}>
                    {searchResults.map((r) => (
                      <li key={r.leagueId} onClick={() => selectLeague(r)}>
                        {r.logo && <img src={r.logo} alt="" className={styles.leagueLogo} />}
                        <span className={styles.leagueName}>{r.name}</span>
                        <span className={styles.leagueMeta}>{r.country} · {r.type}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {selectedLeague && (
                <>
                  <div>
                    <label>Season</label>
                    <select
                      value={importSeason}
                      onChange={(e) => setImportSeason(Number(e.target.value))}
                      required
                    >
                      {selectedLeague.seasons.map((y) => (
                        <option key={y} value={y}>{y}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label>Display name</label>
                    <input
                      value={importName}
                      onChange={(e) => setImportName(e.target.value)}
                      required
                    />
                  </div>
                </>
              )}

              <div className={styles.formActions}>
                <button
                  type="button"
                  className={styles.cancelBtn}
                  onClick={() => setShowImport(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className={styles.saveBtn}
                  disabled={!selectedLeague || importSeason === '' || importing}
                >
                  {importing ? 'Importing…' : 'Import'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
