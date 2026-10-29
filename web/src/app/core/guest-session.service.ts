import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, finalize, map, of, shareReplay, tap } from 'rxjs';
import { API_BASE } from './api-config';
import { GuestSession, GuestSessionResponse } from './models';

const STORAGE_PREFIX = 'djr.guest-session.';

@Injectable({ providedIn: 'root' })
export class GuestSessionService {
  private readonly http = inject(HttpClient);
  private readonly inFlight = new Map<string, Observable<GuestSession>>();

  join(code: string): Observable<GuestSession> {
    const normalized = normalizeCode(code);
    const stored = this.findValidSessionByCode(normalized);
    return stored ? of(stored) : this.create(normalized);
  }

  create(code: string): Observable<GuestSession> {
    const normalized = normalizeCode(code);
    const inFlight = this.inFlight.get(normalized);
    if (inFlight) return inFlight;

    const request$ = this.http
      .post<GuestSessionResponse>(`${API_BASE}/guest-sessions`, { code: normalized })
      .pipe(
        map((response) => ({
          guestToken: response.guestToken,
          expiresAt: response.expiresAt,
          room: response.room,
          code: normalized,
        })),
        tap((session) => this.save(session)),
        finalize(() => this.inFlight.delete(normalized)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    this.inFlight.set(normalized, request$);
    return request$;
  }

  getForRoom(roomId: string): GuestSession | null {
    const session = this.read(roomId);
    if (!session) return null;
    if (isExpired(session.expiresAt)) {
      this.clear(roomId);
      return null;
    }
    return session;
  }

  findValidSessionByCode(code: string): GuestSession | null {
    const normalized = normalizeCode(code);
    const keys = this.storageKeys();
    for (const key of keys) {
      const session = this.readKey(key);
      if (session && session.code === normalized && !isExpired(session.expiresAt)) {
        return session;
      }
    }
    return null;
  }

  save(session: GuestSession): void {
    try {
      sessionStorage.setItem(STORAGE_PREFIX + session.room.id, JSON.stringify(session));
    } catch {
      // Almacenamiento no disponible: no se persiste la sesión.
    }
  }

  updateExpiry(roomId: string, expiresAt: string): void {
    const session = this.read(roomId);
    if (!session) return;
    this.save({ ...session, expiresAt });
  }

  clear(roomId: string): void {
    try {
      sessionStorage.removeItem(STORAGE_PREFIX + roomId);
    } catch {
      // Sin acción.
    }
  }

  private read(roomId: string): GuestSession | null {
    return this.readKey(STORAGE_PREFIX + roomId);
  }

  private readKey(key: string): GuestSession | null {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? (JSON.parse(raw) as GuestSession) : null;
    } catch {
      return null;
    }
  }

  private storageKeys(): string[] {
    const keys: string[] = [];
    try {
      for (let index = 0; index < sessionStorage.length; index += 1) {
        const key = sessionStorage.key(index);
        if (key && key.startsWith(STORAGE_PREFIX)) {
          keys.push(key);
        }
      }
    } catch {
      return [];
    }
    return keys;
  }
}

export function normalizeCode(code: string): string {
  return code.trim().toUpperCase();
}

function isExpired(expiresAt: string): boolean {
  return Date.parse(expiresAt) <= Date.now();
}
