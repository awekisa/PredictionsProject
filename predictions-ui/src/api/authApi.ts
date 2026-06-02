import apiClient from './apiClient';
import type {
  LoginRequest,
  RegisterRequest,
  AuthResponse,
  UpdateUsernameRequest,
  ChangePasswordRequest,
} from '../types';

export const login = (data: LoginRequest) =>
  apiClient.post<AuthResponse>('/auth/login', data).then((res) => res.data);

export const register = (data: RegisterRequest) =>
  apiClient.post<AuthResponse>('/auth/register', data).then((res) => res.data);

export const updateUsername = (data: UpdateUsernameRequest) =>
  apiClient.put<AuthResponse>('/auth/me/username', data).then((res) => res.data);

export const changePassword = (data: ChangePasswordRequest) =>
  apiClient.post<void>('/auth/me/password', data).then((res) => res.data);
