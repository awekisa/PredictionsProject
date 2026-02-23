import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import styles from './Navbar.module.css';

export default function Navbar() {
  const { user, isAdmin, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <nav className={styles.navbar}>
      <div className={styles.links}>
        <Link to="/" className={styles.brand}>
          Predictions
        </Link>
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
      </div>
    </nav>
  );
}
