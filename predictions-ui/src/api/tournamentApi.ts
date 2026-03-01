import apiClient from './apiClient';
import type { TournamentResponse, CompetitionStandingsResponse } from '../types';

export const getTournaments = () =>
  apiClient.get<TournamentResponse[]>('/tournaments').then((res) => res.data);

export const getTournament = (id: number) =>
  apiClient.get<TournamentResponse>(`/tournaments/${id}`).then((res) => res.data);

export const getFootballStandings = (id: number): Promise<CompetitionStandingsResponse | null> =>
  apiClient
    .get<CompetitionStandingsResponse>(`/tournaments/${id}/football-standings`)
    .then((res) => res.data)
    .catch((err) => (err?.response?.status === 204 ? null : null));
