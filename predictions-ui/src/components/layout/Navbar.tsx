import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import styles from './Navbar.module.css';

export default function Navbar() {
  const { user, isAdmin, logout } = useAuth();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const closeMenu = () => setMenuOpen(false);

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
          {isAdmin && (
            <Link to="/admin/tournaments" className={styles.navLink}>
              Admin
            </Link>
          )}
        </div>

        <div className={styles.right}>
          <span className={styles.userName}>{user?.displayName}</span>
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
          {isAdmin && (
            <Link to="/admin/tournaments" className={styles.mobileLink} onClick={closeMenu}>
              Admin
            </Link>
          )}
        </div>
      )}
    </nav>
  );
}
