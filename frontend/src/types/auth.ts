export interface AuthTokensDto {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export interface UserInfoDto {
  userId: string;
  email: string;
  displayName: string | null;
  avatarUrl: string | null;
  createdAt: string;
  lastLoginAt: string | null;
  discordId: string | null;
}

export interface DiscordAuthUrlDto {
  authorizationUrl: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface DiscordCallbackRequest {
  code: string;
}
