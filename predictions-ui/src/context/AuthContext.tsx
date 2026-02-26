import { createContext, useState, type ReactNode } from 'react';
import type { AuthResponse } from '../types';

interface User {
  email: string;
  displayName: string;
  role: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  handleAuthResponse: (response: AuthResponse) => void;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextType>({
  user: null,
  token: null,
  isAuthenticated: false,
  isAdmin: false,
  handleAuthResponse: () => {},
  logout: () => {},
});

function decodeToken(token: string): Record<string, unknown> {
  const base64Url = token.split('.')[1];
  const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
  const jsonPayload = decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
      .join('')
  );
  return JSON.parse(jsonPayload);
}

function getRoleFromToken(token: string): string {
  const payload = decodeToken(token);
  const roleClaim =
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
  if (Array.isArray(roleClaim)) return roleClaim[0] || 'User';
  return (roleClaim as string) || 'User';
}

function isTokenExpired(token: string): boolean {
  const payload = decodeToken(token);
  const exp = payload.exp as number;
  return Date.now() / 1000 > exp;
}

function initUserFromStorage(): User | null {
  try {
    const storedToken = localStorage.getItem('token');
    if (storedToken && !isTokenExpired(storedToken)) {
      const email = localStorage.getItem('userEmail') || '';
      const displayName = localStorage.getItem('userDisplayName') || '';
      const role = getRoleFromToken(storedToken);
      return { email, displayName, role };
    }
  } catch {
    // ignore malformed token
  }
  localStorage.removeItem('token');
  localStorage.removeItem('userEmail');
  localStorage.removeItem('userDisplayName');
  return null;
}

function initTokenFromStorage(): string | null {
  try {
    const storedToken = localStorage.getItem('token');
    if (storedToken && !isTokenExpired(storedToken)) return storedToken;
  } catch {
    // ignore
  }
  return null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(initUserFromStorage);
  const [token, setToken] = useState<string | null>(initTokenFromStorage);

  const handleAuthResponse = (response: AuthResponse) => {
    const role = getRoleFromToken(response.token);
    localStorage.setItem('token', response.token);
    localStorage.setItem('userEmail', response.email);
    localStorage.setItem('userDisplayName', response.displayName);
    setToken(response.token);
    setUser({ email: response.email, displayName: response.displayName, role });
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userEmail');
    localStorage.removeItem('userDisplayName');
    setToken(null);
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!user,
        isAdmin: user?.role === 'Admin',
        handleAuthResponse,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
