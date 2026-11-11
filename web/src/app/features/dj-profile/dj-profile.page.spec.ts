import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GuestRoomService } from '../../core/guest-room.service';
import { GuestSessionService } from '../../core/guest-session.service';
import { DjProfile, GuestSession } from '../../core/models';
import { DjProfilePage } from './dj-profile.page';

const ROOM_ID = '11111111-1111-1111-1111-111111111111';
const CODE = 'ABCDEFGHIJ';

type MockFn = ReturnType<typeof vi.fn>;

function makeProfile(): DjProfile {
  return {
    artistName: 'DJ Luna',
    description: 'Sesiones de house y techno.',
    startYear: 2018,
    photoUrl: '/api/profile-photos/abc.jpg',
    socialLinks: {
      instagram: 'https://instagram.com/djluna',
      facebook: null,
      tiktok: null,
      youtube: null,
      soundcloud: 'https://soundcloud.com/djluna',
    },
  };
}

function makeSession(): GuestSession {
  const room = {
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

  return { guestToken: 'guest-token', expiresAt: room.endsAt, room, code: CODE };
}

describe('DjProfilePage', () => {
  let sessions: { getForRoom: MockFn; clear: MockFn };
  let rooms: { getDjProfile: MockFn };

  function createComponent(): DjProfilePage {
    const fixture = TestBed.createComponent(DjProfilePage);
    return fixture.componentInstance;
  }

  beforeEach(() => {
    sessions = {
      getForRoom: vi.fn().mockReturnValue(makeSession()),
      clear: vi.fn(),
    };
    rooms = {
      getDjProfile: vi.fn().mockReturnValue(of(makeProfile())),
    };

    TestBed.configureTestingModule({
      imports: [DjProfilePage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => ROOM_ID } } },
        },
        { provide: GuestSessionService, useValue: sessions },
        { provide: GuestRoomService, useValue: rooms },
      ],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('loads the DJ profile for the guest session', async () => {
    const component = createComponent();

    await component.init();

    expect(component.profile()?.artistName).toBe('DJ Luna');
    expect(component.roomName()).toBe('Sala de prueba');
    expect(component.socialLinks().map((link) => link.key)).toEqual([
      'instagram',
      'soundcloud',
    ]);
    expect(component.hasDetails()).toBe(true);
  });

  it('requires a guest session for the room', async () => {
    sessions.getForRoom.mockReturnValue(null);
    const component = createComponent();

    await component.init();

    expect(component.sessionExpired()).toBe(true);
    expect(rooms.getDjProfile).not.toHaveBeenCalled();
  });

  it('clears the session when the guest token is rejected', async () => {
    rooms.getDjProfile.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 401 })),
    );
    const component = createComponent();

    await component.init();

    expect(component.sessionExpired()).toBe(true);
    expect(sessions.clear).toHaveBeenCalledWith(ROOM_ID);
  });

  it('shows a server error without expiring the session', async () => {
    rooms.getDjProfile.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );
    const component = createComponent();

    await component.init();

    expect(component.sessionExpired()).toBe(false);
    expect(component.error()).toContain('servidor');
  });
});
