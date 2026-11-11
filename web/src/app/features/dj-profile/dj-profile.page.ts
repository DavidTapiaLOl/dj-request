import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { mapHttpError } from '../../core/api-error';
import { GuestRoomService } from '../../core/guest-room.service';
import { GuestSessionService } from '../../core/guest-session.service';
import { DjProfile, GuestSession, SocialLinks } from '../../core/models';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

const SOCIAL_PLATFORMS: ReadonlyArray<{
  key: keyof SocialLinks;
  label: string;
}> = [
  { key: 'instagram', label: 'Instagram' },
  { key: 'facebook', label: 'Facebook' },
  { key: 'tiktok', label: 'TikTok' },
  { key: 'youtube', label: 'YouTube' },
  { key: 'soundcloud', label: 'SoundCloud' },
];

@Component({
  selector: 'app-dj-profile',
  imports: [RouterLink, BrandLogo],
  templateUrl: './dj-profile.page.html',
  styleUrl: './dj-profile.page.scss',
})
export class DjProfilePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly sessions = inject(GuestSessionService);
  private readonly rooms = inject(GuestRoomService);

  readonly profile = signal<DjProfile | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly sessionExpired = signal(false);
  readonly roomId = signal<string | null>(null);
  readonly roomName = signal<string | null>(null);

  readonly socialLinks = computed(() => {
    const links = this.profile()?.socialLinks;
    if (!links) return [];

    return SOCIAL_PLATFORMS.flatMap((platform) => {
      const url = links[platform.key];
      return url ? [{ ...platform, url }] : [];
    });
  });

  readonly hasDetails = computed(() => {
    const profile = this.profile();
    if (!profile) return false;

    return Boolean(
      profile.description || profile.startYear || this.socialLinks().length > 0,
    );
  });

  private session: GuestSession | null = null;

  ngOnInit(): void {
    void this.init();
  }

  async init(): Promise<void> {
    const roomId = this.route.snapshot.paramMap.get('roomId');
    this.roomId.set(roomId);

    if (!roomId) {
      this.loading.set(false);
      this.sessionExpired.set(true);
      return;
    }

    this.session = this.sessions.getForRoom(roomId);
    if (!this.session) {
      this.loading.set(false);
      this.sessionExpired.set(true);
      return;
    }

    this.roomName.set(this.session.room.name);
    await this.loadProfile();
  }

  async loadProfile(): Promise<void> {
    if (!this.session) {
      this.sessionExpired.set(true);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      const profile = await firstValueFrom(
        this.rooms.getDjProfile(
          this.session.room.id,
          this.session.guestToken,
        ),
      );
      this.profile.set(profile);
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const mapped = mapHttpError(error);
        if (mapped.kind === 'session') {
          this.expireSession();
          return;
        }
        this.error.set(mapped.message);
        return;
      }
      this.error.set('No pudimos cargar el perfil del DJ. Inténtalo de nuevo.');
    } finally {
      this.loading.set(false);
    }
  }

  private expireSession(): void {
    if (this.session) {
      this.sessions.clear(this.session.room.id);
    }
    this.session = null;
    this.sessionExpired.set(true);
  }
}
