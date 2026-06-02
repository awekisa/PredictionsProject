import apiClient from './apiClient';
import type { AdminPredictionResponse, AdminUserResponse } from '../types';

export const getPredictions = () =>
  apiClient.get<AdminPredictionResponse[]>('/admin/predictions').then((res) => res.data);

export const deletePrediction = (id: number) =>
  apiClient.delete(`/admin/predictions/${id}`);

export const getUsers = () =>
  apiClient.get<AdminUserResponse[]>('/admin/users').then((res) => res.data);

export const deleteUser = (id: string) =>
  apiClient.delete(`/admin/users/${id}`);
