import apiClient from './apiClient';
import type { TournamentResponse } from '../types';

export const getTournaments = () =>
  apiClient.get<TournamentResponse[]>('/tournaments').then((res) => res.data);

export const getTournament = (id: number) =>
  apiClient.get<TournamentResponse>(`/tournaments/${id}`).then((res) => res.data);
