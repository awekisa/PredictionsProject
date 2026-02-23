import apiClient from './apiClient';
import type { GameResponse, PredictionResponse } from '../types';

export const getGames = (tournamentId: number) =>
  apiClient.get<GameResponse[]>(`/tournaments/${tournamentId}/games`).then((res) => res.data);

export const getGamePredictions = (gameId: number) =>
  apiClient.get<PredictionResponse[]>(`/games/${gameId}/predictions`).then((res) => res.data);
