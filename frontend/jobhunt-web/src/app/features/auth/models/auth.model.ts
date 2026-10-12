export interface AuthUser {
  id: string;
  email: string;
  emailConfirmed: boolean;
}
export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}
export interface AuthCredentials {
  email: string;
  password: string;
}
