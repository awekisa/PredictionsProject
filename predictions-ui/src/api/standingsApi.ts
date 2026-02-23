import apiClient from './apiClient';
import type { StandingEntryResponse } from '../types';

export const getStandings = (tournamentId: number) =>
  apiClient.get<StandingEntryResponse[]>(`/tournaments/${tournamentId}/standings`).then((res) => res.data);
