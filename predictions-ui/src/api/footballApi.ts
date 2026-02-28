import apiClient from './apiClient';
import type { ImportLeagueRequest, LeagueSearchResult, TournamentResponse } from '../types';

export const searchLeagues = (query: string) =>
  apiClient.get<LeagueSearchResult[]>(`/admin/football/leagues?search=${encodeURIComponent(query)}`).then(r => r.data);

export const importLeague = (req: ImportLeagueRequest) =>
  apiClient.post<{ tournament: TournamentResponse; gamesImported: number }>('/admin/football/import', req).then(r => r.data);

export const syncScores = (tournamentId: number) =>
  apiClient.post<{ updated: number }>(`/admin/football/tournaments/${tournamentId}/sync-scores`).then(r => r.data);
