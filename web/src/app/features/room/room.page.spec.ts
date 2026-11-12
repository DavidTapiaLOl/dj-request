import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GuestRoomService } from '../../core/guest-room.service';
import { GuestSessionService } from '../../core/guest-session.service';
import { GenreService } from '../../core/genre.service';
import {
  CreatePlaylistPayload,
  CreateSolicitudPayload,
  GuestSession,
  Playlist,
  RoomMinimal,
  Solicitud,
  SolicitudPage,
} from '../../core/models';
import { RoomPage } from './room.page';

const ROOM_ID = '11111111-1111-1111-1111-111111111111';
const CODE = 'ABCDEFGHIJ';

type MockFn = ReturnType<typeof vi.fn>;

function makeRoom(): RoomMinimal {
  return {
    id: ROOM_ID,
    name: 'Sala de prueba',
    state: 'abierta',
    startsAt: new Date(Date.now() - 3_600_000).toISOString(),
    endsAt: new Date(Date.now() + 3_600_000).toISOString(),
    timezone: 'America/Mexico_City',
    canReceiveRequests: true,
    maxSolicitudesSesion: 10,
    maxPlaylistsSesion: 2,
    intervaloEnvioSegundos: 30,
  };
}

function makeSession(): GuestSession {
  const room = makeRoom();
  return { guestToken: 'guest-token', expiresAt: room.endsAt, room, code: CODE };
}

function makeSolicitud(nombre = 'Canción'): Solicitud {
  return {
    id: '33333333-3333-3333-3333-333333333333',
    nombre,
    artista: null,
    generoId: null,
    generoNombre: null,
    enlace: null,
    estado: 'pendiente',
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
}

function makePage(items: Solicitud[], page = 1, hasMore = false): SolicitudPage {
  return { items, page, pageSize: 20, totalCount: items.length, hasMore };
}

function makePlaylist(nombre: string | null = 'Mi mezcla'): Playlist {
  return {
    id: '44444444-4444-4444-4444-444444444444',
    nombre,
    enlace: 'https://ejemplo.com/lista',
    createdAt: new Date().toISOString(),
  };
}

function makeProfile() {
  return {
    artistName: 'DJ Prueba',
    description: null,
    startYear: null,
    photoUrl: null,
    socialLinks: {
      instagram: null,
      facebook: null,
      tiktok: null,
      youtube: null,
      soundcloud: null,
    },
  };
}

describe('RoomPage', () => {
  let sessions: { getForRoom: MockFn; clear: MockFn; updateExpiry: MockFn; save: MockFn };
  let rooms: {
    getRoom: MockFn;
    listRequests: MockFn;
    createRequest: MockFn;
    createPlaylist: MockFn;
    getDjProfile: MockFn;
  };
  let genres: { list: MockFn };

  function createComponent(): RoomPage {
    const fixture = TestBed.createComponent(RoomPage);
    return fixture.componentInstance;
  }

  beforeEach(() => {
    sessions = {
      getForRoom: vi.fn().mockReturnValue(makeSession()),
      clear: vi.fn(),
      updateExpiry: vi.fn(),
      save: vi.fn(),
    };
    rooms = {
      getRoom: vi.fn().mockReturnValue(of({ room: makeRoom(), expiresAt: makeRoom().endsAt })),
      listRequests: vi.fn().mockReturnValue(of(makePage([]))),
      createRequest: vi.fn(),
      createPlaylist: vi.fn(),
      getDjProfile: vi.fn().mockReturnValue(of(makeProfile())),
    };
    genres = { list: vi.fn().mockReturnValue(of([{ id: 1, nombre: 'Pop' }])) };

    TestBed.configureTestingModule({
      imports: [RoomPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => ROOM_ID } } },
        },
        { provide: GuestSessionService, useValue: sessions },
        { provide: GuestRoomService, useValue: rooms },
        { provide: GenreService, useValue: genres },
      ],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('does not submit an invalid form', async () => {
    const component = createComponent();
    await component.init();

    await component.onSubmit();

    expect(component.form.invalid).toBe(true);
    expect(rooms.createRequest).not.toHaveBeenCalled();
  });

  it('normalizes empty optional fields to null', async () => {
    rooms.createRequest.mockReturnValueOnce(of(makeSolicitud()));
    const component = createComponent();
    await component.init();

    component.form.setValue({
      nombre: '  Canción  ',
      artista: '   ',
      generoId: null,
      enlace: '   ',
    });
    await component.onSubmit();

    const payload = rooms.createRequest.mock.calls[0][2] as CreateSolicitudPayload;
    expect(payload.nombre).toBe('Canción');
    expect(payload.artista).toBeNull();
    expect(payload.enlace).toBeNull();
    expect(payload.generoId).toBeNull();
  });

  it('reuses the idempotency key when retrying after a network failure', async () => {
    rooms.createRequest
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })))
      .mockReturnValueOnce(of(makeSolicitud()));
    const component = createComponent();
    await component.init();
    component.form.controls.nombre.setValue('Canción');

    await component.onSubmit();
    await component.onSubmit();

    expect(rooms.createRequest).toHaveBeenCalledTimes(2);
    const first = rooms.createRequest.mock.calls[0][2] as CreateSolicitudPayload;
    const second = rooms.createRequest.mock.calls[1][2] as CreateSolicitudPayload;
    expect(second.idempotencyKey).toBe(first.idempotencyKey);
    expect(second.nombre).toBe(first.nombre);
  });

  it('treats changed data as a new submission', async () => {
    rooms.createRequest
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })))
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })));
    const component = createComponent();
    await component.init();

    component.form.controls.nombre.setValue('Canción A');
    await component.onSubmit();
    component.form.controls.nombre.setValue('Canción B');
    await component.onSubmit();

    const first = rooms.createRequest.mock.calls[0][2] as CreateSolicitudPayload;
    const second = rooms.createRequest.mock.calls[1][2] as CreateSolicitudPayload;
    expect(second.idempotencyKey).not.toBe(first.idempotencyKey);
  });

  it('clears the form and reloads the list after a confirmed submission', async () => {
    rooms.createRequest.mockReturnValueOnce(of(makeSolicitud()));
    const component = createComponent();
    await component.init();
    const listCalls = rooms.listRequests.mock.calls.length;

    component.form.controls.nombre.setValue('Canción');
    await component.onSubmit();

    expect(component.form.controls.nombre.value).toBe('');
    expect(component.submitSuccess()).toContain('envió');
    expect(rooms.listRequests.mock.calls.length).toBeGreaterThan(listCalls);
  });

  it('informs quota exhaustion without opening a session', async () => {
    rooms.createRequest.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            error: {
              title: 'Límite de solicitudes alcanzado',
              detail: 'La sesión ya alcanzó el máximo de solicitudes.',
            },
          }),
      ),
    );
    const component = createComponent();
    await component.init();

    component.form.controls.nombre.setValue('Canción');
    await component.onSubmit();

    expect(component.submitError()).toContain('límite');
    expect(sessions.save).not.toHaveBeenCalled();
  });

  it('appends the next page with Cargar más', async () => {
    rooms.listRequests
      .mockReturnValueOnce(of(makePage([makeSolicitud('Primera')], 1, true)))
      .mockReturnValueOnce(of(makePage([makeSolicitud('Segunda')], 2, false)));
    const component = createComponent();
    await component.init();

    await component.loadMore();

    expect(component.items().map((item) => item.nombre)).toEqual(['Primera', 'Segunda']);
    expect(component.hasMore()).toBe(false);
  });

  it('requires a new entry when there is no stored session', async () => {
    sessions.getForRoom.mockReturnValue(null);
    const component = createComponent();

    await component.init();

    expect(component.sessionExpired()).toBe(true);
    expect(rooms.getRoom).not.toHaveBeenCalled();
  });

  it('loads the DJ artist name without blocking the room', async () => {
    const component = createComponent();

    await component.init();

    expect(component.djName()).toBe('DJ Prueba');
    expect(rooms.getDjProfile).toHaveBeenCalledWith(ROOM_ID, 'guest-token');
  });

  it('keeps the room usable when the DJ profile cannot be loaded', async () => {
    rooms.getDjProfile.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );
    const component = createComponent();

    await component.init();

    expect(component.room()?.name).toBe('Sala de prueba');
    expect(component.djName()).toBeNull();
    expect(component.roomError()).toBeNull();
    expect(component.sessionExpired()).toBe(false);
  });

  // --- Playlists ---

  it('starts in song mode and switches to the playlist form', async () => {
    const component = createComponent();
    await component.init();

    expect(component.mode()).toBe('song');

    component.setMode('playlist');

    expect(component.mode()).toBe('playlist');
    expect(component.playlistForm.controls.enlace.invalid).toBe(true);
    expect(component.playlistForm.controls.nombre.valid).toBe(true);
  });

  it('keeps the playlist draft when switching between forms', async () => {
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.setValue({ nombre: 'Borrador', enlace: 'https://ejemplo.com/draft' });
    component.setMode('song');
    component.setMode('playlist');

    expect(component.playlistForm.controls.nombre.value).toBe('Borrador');
    expect(component.playlistForm.controls.enlace.value).toBe('https://ejemplo.com/draft');
  });

  it('does not submit an invalid playlist form', async () => {
    const component = createComponent();
    await component.init();
    component.setMode('playlist');
    component.playlistForm.controls.nombre.setValue('Solo nombre sin enlace');

    await component.onSubmitPlaylist();

    expect(component.playlistForm.invalid).toBe(true);
    expect(rooms.createPlaylist).not.toHaveBeenCalled();
  });

  it('rejects a playlist link without host or with credentials', async () => {
    const component = createComponent();
    await component.init();

    component.playlistForm.controls.enlace.setValue('ftp://ejemplo.com/lista');
    expect(component.playlistForm.controls.enlace.invalid).toBe(true);

    component.playlistForm.controls.enlace.setValue('https://user:pass@ejemplo.com/lista');
    expect(component.playlistForm.controls.enlace.invalid).toBe(true);

    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista');
    expect(component.playlistForm.controls.enlace.valid).toBe(true);

    expect(rooms.createPlaylist).not.toHaveBeenCalled();
  });

  it('submits a playlist, confirms privacy and keeps the public list untouched', async () => {
    rooms.createPlaylist.mockReturnValueOnce(of(makePlaylist()));
    const component = createComponent();
    await component.init();
    const listCalls = rooms.listRequests.mock.calls.length;

    component.setMode('playlist');
    component.playlistForm.setValue({
      nombre: '  Mi mezcla  ',
      enlace: '  https://ejemplo.com/lista  ',
    });
    await component.onSubmitPlaylist();

    const payload = rooms.createPlaylist.mock.calls[0][2] as CreatePlaylistPayload;
    expect(payload.nombre).toBe('Mi mezcla');
    expect(payload.enlace).toBe('https://ejemplo.com/lista');
    expect(payload.idempotencyKey).toBeTruthy();
    expect(component.playlistSuccess()).toContain('Solo el DJ podrá ver esta playlist');
    expect(component.playlistForm.controls.nombre.value).toBe('');
    expect(rooms.listRequests.mock.calls.length).toBe(listCalls);
  });

  it('normalizes a blank playlist name to null', async () => {
    rooms.createPlaylist.mockReturnValueOnce(of(makePlaylist(null)));
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.setValue({ nombre: '   ', enlace: 'https://ejemplo.com/lista' });
    await component.onSubmitPlaylist();

    const payload = rooms.createPlaylist.mock.calls[0][2] as CreatePlaylistPayload;
    expect(payload.nombre).toBeNull();
    expect(payload.enlace).toBe('https://ejemplo.com/lista');
  });

  it('reuses the playlist idempotency key when retrying after a network failure', async () => {
    rooms.createPlaylist
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })))
      .mockReturnValueOnce(of(makePlaylist()));
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista');

    await component.onSubmitPlaylist();
    await component.onSubmitPlaylist();

    expect(rooms.createPlaylist).toHaveBeenCalledTimes(2);
    const first = rooms.createPlaylist.mock.calls[0][2] as CreatePlaylistPayload;
    const second = rooms.createPlaylist.mock.calls[1][2] as CreatePlaylistPayload;
    expect(second.idempotencyKey).toBe(first.idempotencyKey);
    expect(second.enlace).toBe(first.enlace);
  });

  it('treats changed playlist data as a new submission', async () => {
    rooms.createPlaylist
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })))
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })));
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista-a');
    await component.onSubmitPlaylist();
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista-b');
    await component.onSubmitPlaylist();

    const first = rooms.createPlaylist.mock.calls[0][2] as CreatePlaylistPayload;
    const second = rooms.createPlaylist.mock.calls[1][2] as CreatePlaylistPayload;
    expect(second.idempotencyKey).not.toBe(first.idempotencyKey);
  });

  it('informs playlist quota exhaustion without reopening a session', async () => {
    rooms.createPlaylist.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            error: {
              title: 'Límite de playlists alcanzado',
              detail: 'Se alcanzó el límite de 2 playlists por sesión.',
            },
          }),
      ),
    );
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista');
    await component.onSubmitPlaylist();

    expect(component.playlistError()).toContain('límite de 2 playlists');
    expect(component.playlistError()).toContain('No se creó otra sesión');
    expect(sessions.save).not.toHaveBeenCalled();
  });

  it('shows the shared interval wait on a playlist submission', async () => {
    rooms.createPlaylist.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            headers: new HttpHeaders({ 'Retry-After': '30' }),
            error: {
              title: 'Demasiadas solicitudes',
              detail: 'Debe esperar 30 segundos entre envíos.',
            },
          }),
      ),
    );
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista');
    await component.onSubmitPlaylist();

    expect(component.playlistError()).toContain('Espera 30 segundos');
  });

  it('clears the playlist form errors on a new submission', async () => {
    rooms.createPlaylist
      .mockReturnValueOnce(of(makePlaylist()))
      .mockReturnValueOnce(of(makePlaylist()));
    const component = createComponent();
    await component.init();

    component.setMode('playlist');
    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/lista');
    await component.onSubmitPlaylist();
    expect(component.playlistSuccess()).not.toBeNull();

    component.playlistForm.controls.enlace.setValue('https://ejemplo.com/otra');
    await component.onSubmitPlaylist();

    expect(component.playlistError()).toBeNull();
    expect(component.playlistSuccess()).toContain('envió correctamente');
  });
});
