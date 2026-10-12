import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { AuthCredentials, AuthSession, AuthUser } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly channel =
    typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('jobhunt-auth') : null;
  readonly user = signal<AuthUser | null>(null);
  private token: string | null = null;
  private expiresAt = 0;
  private refreshing: Promise<string> | null = null;
  private generation = 0;

  constructor() {
    if (this.channel)
      this.channel.onmessage = () => {
        this.clear();
        void this.router.navigate(['/login']);
      };
  }

  register(credentials: AuthCredentials) {
    return firstValueFrom(this.http.post<AuthUser>('/api/auth/register', credentials));
  }

  async login(credentials: AuthCredentials): Promise<void> {
    const session = await firstValueFrom(
      this.http.post<AuthSession>('/api/auth/login', credentials),
    );
    this.generation++;
    this.accept(session);
    this.channel?.postMessage('session-changed');
  }

  async ensureToken(force = false, failedToken?: string): Promise<string> {
    if (
      this.token &&
      ((!force && this.expiresAt > Date.now() + 10000) ||
        (force && failedToken !== undefined && failedToken !== this.token))
    )
      return this.token;
    if (this.refreshing) return this.refreshing;
    const generation = this.generation;
    const operation = async () => {
      const session = await firstValueFrom(this.http.post<AuthSession>('/api/auth/refresh', {}));
      if (generation !== this.generation) throw new Error('Session changed during refresh.');
      this.accept(session);
      return session.accessToken;
    };
    // Serialize cookie rotation across browser tabs as well as requests within this tab.
    const pending = navigator.locks
      ? navigator.locks.request('jobhunt-session-refresh', operation)
      : operation();
    this.refreshing = pending
      .catch((error) => {
        if (generation === this.generation) this.clear();
        throw error;
      })
      .finally(() => {
        this.refreshing = null;
      });
    return this.refreshing;
  }

  async logout(): Promise<void> {
    const operation = async () => {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
      this.clear();
      this.channel?.postMessage('signed-out');
    };
    if (navigator.locks) await navigator.locks.request('jobhunt-session-refresh', operation);
    else await operation();
  }

  clear(): void {
    this.generation++;
    this.token = null;
    this.expiresAt = 0;
    this.user.set(null);
  }

  private accept(session: AuthSession): void {
    this.token = session.accessToken;
    this.expiresAt = Date.parse(session.expiresAtUtc);
    this.user.set(session.user);
  }
}
