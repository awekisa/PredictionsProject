import apiClient from './apiClient';
import type { LoginRequest, RegisterRequest, AuthResponse } from '../types';

export const login = (data: LoginRequest) =>
  apiClient.post<AuthResponse>('/auth/login', data).then((res) => res.data);

export const register = (data: RegisterRequest) =>
  apiClient.post<AuthResponse>('/auth/register', data).then((res) => res.data);
