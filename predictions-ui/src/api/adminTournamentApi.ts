import apiClient from './apiClient';
import type { TournamentResponse, CreateTournamentRequest, UpdateTournamentRequest } from '../types';

export const getAll = () =>
  apiClient.get<TournamentResponse[]>('/admin/tournaments').then((res) => res.data);

export const create = (data: CreateTournamentRequest) =>
  apiClient.post<TournamentResponse>('/admin/tournaments', data).then((res) => res.data);

export const update = (id: number, data: UpdateTournamentRequest) =>
  apiClient.put<TournamentResponse>(`/admin/tournaments/${id}`, data).then((res) => res.data);

export const remove = (id: number) =>
  apiClient.delete(`/admin/tournaments/${id}`);
