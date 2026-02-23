import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './components/common/ProtectedRoute';
import AdminRoute from './components/common/AdminRoute';
import AppLayout from './components/layout/AppLayout';
import LoginPage from './components/auth/LoginPage';
import RegisterPage from './components/auth/RegisterPage';
import TournamentListPage from './components/tournaments/TournamentListPage';
import TournamentDetailPage from './components/tournaments/TournamentDetailPage';
import GamePredictionsPage from './components/games/GamePredictionsPage';
import AdminTournamentsPage from './components/admin/AdminTournamentsPage';
import AdminGamesPage from './components/admin/AdminGamesPage';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/" element={<TournamentListPage />} />
          <Route path="/tournaments/:id" element={<TournamentDetailPage />} />
          <Route path="/games/:gameId/predictions" element={<GamePredictionsPage />} />

          <Route element={<AdminRoute />}>
            <Route path="/admin/tournaments" element={<AdminTournamentsPage />} />
            <Route path="/admin/tournaments/:tournamentId/games" element={<AdminGamesPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
