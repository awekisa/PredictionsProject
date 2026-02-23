export interface AuthResponse {
  token: string;
  email: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface TournamentResponse {
  id: number;
  name: string;
  createdAt: string;
}

export interface CreateTournamentRequest {
  name: string;
}

export interface UpdateTournamentRequest {
  name: string;
}

export interface GameResponse {
  id: number;
  tournamentId: number;
  homeTeam: string;
  awayTeam: string;
  startTime: string;
  homeGoals: number | null;
  awayGoals: number | null;
}

export interface CreateGameRequest {
  homeTeam: string;
  awayTeam: string;
  startTime: string;
}

export interface UpdateGameRequest {
  homeTeam: string;
  awayTeam: string;
  startTime: string;
}

export interface SetGameResultRequest {
  homeGoals: number;
  awayGoals: number;
}

export interface PredictionResponse {
  id: number;
  gameId: number;
  homeTeam: string;
  awayTeam: string;
  homeGoals: number;
  awayGoals: number;
  userDisplayName: string;
  createdAt: string;
}

export interface PlacePredictionRequest {
  homeGoals: number;
  awayGoals: number;
}

export interface StandingEntryResponse {
  position: number;
  userDisplayName: string;
  points: number;
  correctScores: number;
  totalPredictions: number;
}
