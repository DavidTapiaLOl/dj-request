import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GuestSessionService } from './guest-session.service';
import { GuestSession, RoomMinimal } from './models';

const ROOM_ID = '11111111-1111-1111-1111-111111111111';
const CODE = 'ABCDEFGHIJ';

function makeRoom(id: string): RoomMinimal {
  return {
    id,
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

function makeSession(roomId = ROOM_ID, token = 'token-1', code = CODE): GuestSession {
  const room = makeRoom(roomId);
  return { guestToken: token, expiresAt: room.endsAt, room, code };
}

describe('GuestSessionService', () => {
  let service: GuestSessionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(GuestSessionService);
    httpMock = TestBed.inject(HttpTestingController);
    sessionStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates a session and stores it by room', () => {
    const session = makeSession();
    let result: GuestSession | undefined;

    service.create(CODE.toLowerCase()).subscribe((value) => (result = value));

    const request = httpMock.expectOne('/api/guest-sessions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ code: CODE });
    request.flush({
      guestToken: session.guestToken,
      expiresAt: session.expiresAt,
      room: session.room,
    });

    expect(result?.guestToken).toBe('token-1');
    expect(service.getForRoom(ROOM_ID)?.guestToken).toBe('token-1');
    expect(service.getForRoom('22222222-2222-2222-2222-222222222222')).toBeNull();
  });

  it('reuses a stored valid session instead of creating another', () => {
    service.save(makeSession());
    let result: GuestSession | undefined;

    service.join(CODE).subscribe((value) => (result = value));

    httpMock.expectNone('/api/guest-sessions');
    expect(result?.guestToken).toBe('token-1');
  });

  it('deduplicates concurrent joins for the same code', () => {
    const results: GuestSession[] = [];
    service.join(CODE).subscribe((value) => results.push(value));
    service.join(CODE).subscribe((value) => results.push(value));

    const request = httpMock.expectOne('/api/guest-sessions');
    request.flush({
      guestToken: 'token-2',
      expiresAt: makeRoom(ROOM_ID).endsAt,
      room: makeRoom(ROOM_ID),
    });

    expect(results).toHaveLength(2);
    expect(results[0].guestToken).toBe('token-2');
    expect(results[1].guestToken).toBe('token-2');
  });

  it('ignores and clears an expired session', () => {
    const expired = makeSession();
    expired.expiresAt = new Date(Date.now() - 1000).toISOString();
    service.save(expired);

    expect(service.getForRoom(ROOM_ID)).toBeNull();
    expect(service.findValidSessionByCode(CODE)).toBeNull();
  });
});
