import apiClient from './apiClient';
import type { StandingEntryResponse, UserPredictionDetailResponse } from '../types';

export const getStandings = (tournamentId: number) =>
  apiClient.get<StandingEntryResponse[]>(`/tournaments/${tournamentId}/standings`).then((res) => res.data);

export const getUserPredictionDetails = (tournamentId: number, userDisplayName: string, type: string) =>
  apiClient.get<UserPredictionDetailResponse[]>(
    `/tournaments/${tournamentId}/standings/${encodeURIComponent(userDisplayName)}/predictions`,
    { params: { type } }
  ).then((res) => res.data);
