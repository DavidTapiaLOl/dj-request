import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { mapHttpError } from '../../core/api-error';
import { GuestRoomService } from '../../core/guest-room.service';
import { GuestSessionService } from '../../core/guest-session.service';
import { GenreService } from '../../core/genre.service';
import { newIdempotencyKey } from '../../core/ids';
import { formatSchedule, roomStateLabel, solicitudEstadoLabel } from '../../core/labels';
import {
  CreatePlaylistInput,
  CreateSolicitudInput,
  Genero,
  GuestSession,
  RoomMinimal,
  Solicitud,
} from '../../core/models';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

const HTTP_URL_PATTERN = /^https?:\/\/\S+$/i;
const PAGE_SIZE = 20;

@Component({
  selector: 'app-room',
  imports: [ReactiveFormsModule, RouterLink, BrandLogo],
  templateUrl: './room.page.html',
  styleUrl: './room.page.scss',
})
export class RoomPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly sessions = inject(GuestSessionService);
  private readonly rooms = inject(GuestRoomService);
  private readonly genreService = inject(GenreService);
  private readonly formBuilder = inject(FormBuilder);

  readonly form = this.formBuilder.nonNullable.group({
    nombre: ['', [nonBlankValidator, Validators.maxLength(200)]],
    artista: ['', [Validators.maxLength(200)]],
    generoId: [null as number | null],
    enlace: ['', [Validators.maxLength(2048), httpUrlValidator]],
  });

  readonly playlistForm = this.formBuilder.nonNullable.group({
    nombre: ['', [Validators.maxLength(200)]],
    enlace: ['', [nonBlankValidator, Validators.maxLength(2048), playlistUrlValidator]],
  });

  readonly mode = signal<'song' | 'playlist'>('song');

  readonly room = signal<RoomMinimal | null>(null);
  readonly djName = signal<string | null>(null);
  readonly items = signal<Solicitud[]>([]);
  readonly genres = signal<Genero[]>([]);
  readonly totalCount = signal(0);
  readonly hasMore = signal(false);
  readonly page = signal(1);

  readonly loadingRoom = signal(true);
  readonly loadingList = signal(false);
  readonly loadingMore = signal(false);
  readonly submitting = signal(false);
  readonly playlistSubmitting = signal(false);

  readonly sending = computed(() => this.submitting() || this.playlistSubmitting());

  readonly sessionExpired = signal(false);
  readonly roomError = signal<string | null>(null);
  readonly listError = signal<string | null>(null);
  readonly submitError = signal<string | null>(null);
  readonly submitSuccess = signal<string | null>(null);
  readonly playlistError = signal<string | null>(null);
  readonly playlistSuccess = signal<string | null>(null);
  readonly genresError = signal(false);
  readonly submitted = signal(false);
  readonly playlistSubmitted = signal(false);

  readonly schedule = computed(() => {
    const room = this.room();
    return room ? formatSchedule(room.startsAt, room.endsAt, room.timezone) : '';
  });

  readonly roomState = computed(() => {
    const room = this.room();
    return room ? roomStateLabel(room.state) : '';
  });

  readonly roomStateClass = computed(() => {
    const state = this.room()?.state;
    return state ? `badge badge--${state}` : 'badge';
  });

  readonly unavailableReason = computed(() => {
    const room = this.room();
    if (!room || room.canReceiveRequests) return null;
    if (room.state === 'pausada') {
      return 'La sala está pausada. El DJ no está aceptando solicitudes en este momento.';
    }
    if (room.state === 'cerrada') {
      return 'La sala está cerrada y ya no admite solicitudes.';
    }
    return 'La sala no está recibiendo solicitudes en este momento.';
  });

  private session: GuestSession | null = null;
  private pendingAttempt: { fingerprint: string; idempotencyKey: string } | null = null;
  private pendingPlaylistAttempt: { fingerprint: string; idempotencyKey: string } | null = null;

  ngOnInit(): void {
    void this.init();
  }

  async init(): Promise<void> {
    const roomId = this.route.snapshot.paramMap.get('roomId');
    if (!roomId) {
      this.loadingRoom.set(false);
      this.sessionExpired.set(true);
      return;
    }

    this.session = this.sessions.getForRoom(roomId);
    if (!this.session) {
      this.loadingRoom.set(false);
      this.sessionExpired.set(true);
      return;
    }

    await Promise.all([
      this.loadRoom(),
      this.loadGenres(),
      this.reloadList(),
      this.loadDjName(),
    ]);
  }

  async refresh(): Promise<void> {
    await Promise.all([this.loadRoom(), this.reloadList()]);
  }

  async loadMore(): Promise<void> {
    if (!this.hasMore() || this.loadingList() || this.loadingMore()) return;
    await this.fetchList(this.page() + 1, false);
  }

  async onSubmit(): Promise<void> {
    if (this.sending()) return;

    this.submitted.set(true);
    this.submitError.set(null);
    this.submitSuccess.set(null);

    if (!this.session) {
      this.sessionExpired.set(true);
      return;
    }

    if (this.form.invalid) return;

    const input = this.buildInput();
    const fingerprint = JSON.stringify(input);
    const idempotencyKey =
      this.pendingAttempt?.fingerprint === fingerprint
        ? this.pendingAttempt.idempotencyKey
        : newIdempotencyKey();
    this.pendingAttempt = { fingerprint, idempotencyKey };

    this.submitting.set(true);
    try {
      await firstValueFrom(
        this.rooms.createRequest(this.session.room.id, this.session.guestToken, {
          ...input,
          idempotencyKey,
        }),
      );
      this.pendingAttempt = null;
      this.form.reset({ nombre: '', artista: '', generoId: null, enlace: '' });
      this.submitted.set(false);
      this.submitSuccess.set('Tu canción se envió correctamente.');
      await this.reloadList();
    } catch (error) {
      this.handleSubmitError(error);
    } finally {
      this.submitting.set(false);
    }
  }

  setMode(mode: 'song' | 'playlist'): void {
    if (this.sending()) return;
    this.mode.set(mode);
  }

  async onSubmitPlaylist(): Promise<void> {
    if (this.sending()) return;

    this.playlistSubmitted.set(true);
    this.playlistError.set(null);
    this.playlistSuccess.set(null);

    if (!this.session) {
      this.sessionExpired.set(true);
      return;
    }

    if (this.playlistForm.invalid) return;

    const input = this.buildPlaylistInput();
    const fingerprint = JSON.stringify(input);
    const idempotencyKey =
      this.pendingPlaylistAttempt?.fingerprint === fingerprint
        ? this.pendingPlaylistAttempt.idempotencyKey
        : newIdempotencyKey();
    this.pendingPlaylistAttempt = { fingerprint, idempotencyKey };

    this.playlistSubmitting.set(true);
    try {
      await firstValueFrom(
        this.rooms.createPlaylist(this.session.room.id, this.session.guestToken, {
          ...input,
          idempotencyKey,
        }),
      );
      this.pendingPlaylistAttempt = null;
      this.playlistForm.reset({ nombre: '', enlace: '' });
      this.playlistSubmitted.set(false);
      this.playlistSuccess.set('Tu playlist se envió correctamente. Solo el DJ podrá ver esta playlist.');
    } catch (error) {
      this.handlePlaylistSubmitError(error);
    } finally {
      this.playlistSubmitting.set(false);
    }
  }

  showError(field: 'nombre' | 'artista' | 'enlace'): boolean {
    const control = this.form.controls[field];
    return control.invalid && (control.touched || this.submitted());
  }

  errorFor(field: 'nombre' | 'artista' | 'enlace'): string {
    const control = this.form.controls[field];
    if (control.hasError('required')) return 'El nombre de la canción es obligatorio.';
    if (control.hasError('maxlength')) {
      return field === 'enlace'
        ? 'El enlace no puede superar 2048 caracteres.'
        : 'Máximo 200 caracteres.';
    }
    if (control.hasError('pattern')) return 'El enlace debe empezar por http:// o https://.';
    return 'Revisa este campo.';
  }

  showPlaylistError(field: 'nombre' | 'enlace'): boolean {
    const control = this.playlistForm.controls[field];
    return control.invalid && (control.touched || this.playlistSubmitted());
  }

  errorPlaylistFor(field: 'nombre' | 'enlace'): string {
    const control = this.playlistForm.controls[field];
    if (control.hasError('required')) {
      return field === 'enlace'
        ? 'El enlace de la playlist es obligatorio.'
        : 'Este campo es obligatorio.';
    }
    if (control.hasError('maxlength')) {
      return field === 'enlace'
        ? 'El enlace no puede superar 2048 caracteres.'
        : 'Máximo 200 caracteres.';
    }
    if (control.hasError('pattern')) {
      return 'El enlace debe ser una URL http:// o https:// válida, con dominio y sin credenciales.';
    }
    return 'Revisa este campo.';
  }

  estadoLabel(estado: string): string {
    return solicitudEstadoLabel(estado);
  }

  estadoClass(estado: string): string {
    return `badge badge--${estado}`;
  }

  createdLabel(createdAt: string): string {
    const date = new Date(createdAt);
    return Number.isNaN(date.getTime())
      ? ''
      : date.toLocaleString('es-MX', { dateStyle: 'medium', timeStyle: 'short' });
  }

  private buildInput(): CreateSolicitudInput {
    const value = this.form.getRawValue();
    return {
      nombre: value.nombre.trim(),
      artista: optionalText(value.artista),
      generoId: value.generoId ?? null,
      enlace: optionalText(value.enlace),
    };
  }

  private buildPlaylistInput(): CreatePlaylistInput {
    const value = this.playlistForm.getRawValue();
    return {
      nombre: optionalText(value.nombre),
      enlace: value.enlace.trim(),
    };
  }

  private handlePlaylistSubmitError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.playlistError.set('Ocurrió un error inesperado. Inténtalo de nuevo.');
      return;
    }

    const mapped = mapHttpError(error);
    if (mapped.kind === 'session') {
      this.expireSession();
      return;
    }

    if (mapped.kind === 'network' || mapped.kind === 'server') {
      this.playlistError.set(`${mapped.message} Puedes reintentar sin cambiar los datos.`);
      return;
    }

    this.pendingPlaylistAttempt = null;

    if (mapped.kind === 'quota') {
      const maxPlaylists = this.room()?.maxPlaylistsSesion ?? 2;
      this.playlistError.set(
        `Has alcanzado el límite de ${maxPlaylists} playlists de esta sesión. No se creó otra sesión.`,
      );
      return;
    }

    if (mapped.kind === 'rateLimited' && mapped.retryAfterSeconds !== null) {
      this.playlistError.set(`${mapped.message} Espera ${mapped.retryAfterSeconds} segundos.`);
      return;
    }

    const fieldMessage = mapped.fieldErrors['Enlace'] ?? mapped.fieldErrors['Nombre'];
    this.playlistError.set(fieldMessage || mapped.message);
  }

  private handleSubmitError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.submitError.set('Ocurrió un error inesperado. Inténtalo de nuevo.');
      return;
    }

    const mapped = mapHttpError(error);
    if (mapped.kind === 'session') {
      this.expireSession();
      return;
    }

    if (mapped.kind === 'network' || mapped.kind === 'server') {
      this.submitError.set(`${mapped.message} Puedes reintentar sin cambiar los datos.`);
      return;
    }

    this.pendingAttempt = null;

    if (mapped.kind === 'quota') {
      this.submitError.set(
        'Has alcanzado el límite de solicitudes de esta sesión. No se creó otra sesión.',
      );
      return;
    }

    if (mapped.kind === 'rateLimited' && mapped.retryAfterSeconds !== null) {
      this.submitError.set(`${mapped.message} Espera ${mapped.retryAfterSeconds} segundos.`);
      return;
    }

    this.submitError.set(mapped.message);
  }

  private async loadRoom(): Promise<void> {
    if (!this.session) return;

    this.loadingRoom.set(true);
    this.roomError.set(null);
    try {
      const response = await firstValueFrom(
        this.rooms.getRoom(this.session.room.id, this.session.guestToken),
      );
      this.room.set(response.room);
      this.sessions.updateExpiry(this.session.room.id, response.expiresAt);
      this.session = { ...this.session, expiresAt: response.expiresAt, room: response.room };
    } catch (error) {
      this.handleLoadError(error, (message) => this.roomError.set(message));
    } finally {
      this.loadingRoom.set(false);
    }
  }

  private async loadGenres(): Promise<void> {
    try {
      this.genres.set(await firstValueFrom(this.genreService.list()));
      this.genresError.set(false);
    } catch {
      this.genresError.set(true);
    }
  }

  private async loadDjName(): Promise<void> {
    if (!this.session) return;

    try {
      const profile = await firstValueFrom(
        this.rooms.getDjProfile(this.session.room.id, this.session.guestToken),
      );
      this.djName.set(profile.artistName.trim() || null);
    } catch {
      this.djName.set(null);
    }
  }

  private async reloadList(): Promise<void> {
    await this.fetchList(1, true);
  }

  private async fetchList(page: number, replace: boolean): Promise<void> {
    if (!this.session) return;

    if (replace) {
      this.loadingList.set(true);
    } else {
      this.loadingMore.set(true);
    }
    this.listError.set(null);

    try {
      const response = await firstValueFrom(
        this.rooms.listRequests(this.session.room.id, this.session.guestToken, page, PAGE_SIZE),
      );
      this.items.set(replace ? response.items : [...this.items(), ...response.items]);
      this.page.set(response.page);
      this.hasMore.set(response.hasMore);
      this.totalCount.set(response.totalCount);
    } catch (error) {
      this.handleLoadError(error, (message) => this.listError.set(message));
    } finally {
      this.loadingList.set(false);
      this.loadingMore.set(false);
    }
  }

  private handleLoadError(error: unknown, setMessage: (message: string) => void): void {
    if (error instanceof HttpErrorResponse) {
      const mapped = mapHttpError(error);
      if (mapped.kind === 'session') {
        this.expireSession();
        return;
      }
      setMessage(mapped.message);
      return;
    }
    setMessage('No pudimos cargar la información. Inténtalo de nuevo.');
  }

  private expireSession(): void {
    if (this.session) this.sessions.clear(this.session.room.id);
    this.session = null;
    this.sessionExpired.set(true);
  }
}

function optionalText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function nonBlankValidator(control: AbstractControl): ValidationErrors | null {
  const value = typeof control.value === 'string' ? control.value : '';
  return value.trim().length > 0 ? null : { required: true };
}

function httpUrlValidator(control: AbstractControl): ValidationErrors | null {
  const value = typeof control.value === 'string' ? control.value.trim() : '';
  if (value.length === 0) return null;
  return HTTP_URL_PATTERN.test(value) ? null : { pattern: true };
}

// Validacion estricta del enlace de playlist: coincide con las reglas del
// backend (URL absoluta http/https, con dominio y sin credenciales).
function playlistUrlValidator(control: AbstractControl): ValidationErrors | null {
  const value = typeof control.value === 'string' ? control.value.trim() : '';
  if (value.length === 0) return null;

  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return { pattern: true };
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') return { pattern: true };
  if (!url.host) return { pattern: true };
  if (url.username || url.password) return { pattern: true };
  return null;
}
