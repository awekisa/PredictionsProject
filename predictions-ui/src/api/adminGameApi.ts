import apiClient from './apiClient';
import type { GameResponse, CreateGameRequest, UpdateGameRequest, SetGameResultRequest } from '../types';

export const getAll = (tournamentId: number) =>
  apiClient.get<GameResponse[]>(`/admin/tournaments/${tournamentId}/games`).then((res) => res.data);

export const create = (tournamentId: number, data: CreateGameRequest) =>
  apiClient.post<GameResponse>(`/admin/tournaments/${tournamentId}/games`, data).then((res) => res.data);

export const update = (tournamentId: number, gameId: number, data: UpdateGameRequest) =>
  apiClient.put<GameResponse>(`/admin/tournaments/${tournamentId}/games/${gameId}`, data).then((res) => res.data);

export const remove = (tournamentId: number, gameId: number) =>
  apiClient.delete(`/admin/tournaments/${tournamentId}/games/${gameId}`);

export const setResult = (gameId: number, data: SetGameResultRequest) =>
  apiClient.put<GameResponse>(`/admin/games/${gameId}/result`, data).then((res) => res.data);
