import apiClient from './apiClient';
import type { PlacePredictionRequest, PredictionResponse } from '../types';

export const placePrediction = (gameId: number, data: PlacePredictionRequest) =>
  apiClient.post<PredictionResponse>(`/games/${gameId}/predictions`, data).then((res) => res.data);

export const getMyPredictions = (tournamentId: number) =>
  apiClient.get<PredictionResponse[]>(`/tournaments/${tournamentId}/my-predictions`).then((res) => res.data);
