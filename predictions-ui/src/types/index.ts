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
  externalLeagueId: number | null;
  externalSeason: number | null;
  emblemUrl: string | null;
}

export interface LeagueSearchResult {
  leagueId: number;
  name: string;
  country: string;
  type: string;
  logo?: string;
  seasons: number[];
}

export interface ImportLeagueRequest {
  leagueId: number;
  season: number;
  name: string;
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
  isFinished: boolean;
  homeCrestUrl: string | null;
  awayCrestUrl: string | null;
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
