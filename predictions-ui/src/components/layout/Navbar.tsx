import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { useTheme } from '../../context/ThemeContext';
import AccountDrawer from '../account/AccountDrawer';
import styles from './Navbar.module.css';

export default function Navbar() {
  const { user, isAdmin, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const closeMenu = () => setMenuOpen(false);
  const initials = (user?.displayName ?? '?')
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => word[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return (
    <nav className={styles.navbar}>
      <div className={styles.navRow}>
        <Link to="/" className={styles.brand} onClick={closeMenu}>
          Predictions
        </Link>

        <div className={styles.desktopLinks}>
          <Link to="/" className={styles.navLink}>
            Tournaments
          </Link>
          <Link to="/standings" className={styles.navLink}>
            Global Standings
          </Link>
          {isAdmin && (
            <Link to="/admin/tournaments" className={styles.navLink}>
              Admin
            </Link>
          )}
        </div>

        <div className={styles.right}>
          <button
            className={styles.userBtn}
            data-open={accountOpen}
            onClick={() => setAccountOpen(true)}
            aria-haspopup="dialog"
            aria-expanded={accountOpen}
          >
            <span className={styles.avatar} aria-hidden="true">
              {initials}
            </span>
            <span className={styles.userMeta}>
              <span className={styles.userNameTxt}>{user?.displayName}</span>
              {isAdmin && <span className={styles.userRole}>Admin</span>}
            </span>
          </button>
          <button className={styles.themeBtn} onClick={toggleTheme} aria-label="Toggle theme">
            {theme === 'dark' ? '☀' : '☾'}
          </button>
          <button className={styles.logoutBtn} onClick={handleLogout}>
            Logout
          </button>
          <button
            className={styles.hamburger}
            onClick={() => setMenuOpen((o) => !o)}
            aria-label="Toggle menu"
            aria-expanded={menuOpen}
          >
            {menuOpen ? '✕' : '☰'}
          </button>
        </div>
      </div>

      {menuOpen && (
        <div className={styles.mobileMenu}>
          <Link to="/" className={styles.mobileLink} onClick={closeMenu}>
            Tournaments
          </Link>
          <Link to="/standings" className={styles.mobileLink} onClick={closeMenu}>
            Global Standings
          </Link>
          {isAdmin && (
            <Link to="/admin/tournaments" className={styles.mobileLink} onClick={closeMenu}>
              Admin
            </Link>
          )}
        </div>
      )}

      <AccountDrawer open={accountOpen} onClose={() => setAccountOpen(false)} />
    </nav>
  );
}
