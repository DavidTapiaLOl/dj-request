import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GuestSessionService } from '../../core/guest-session.service';
import { JoinCodePage } from './join-code.page';

const ROOM_ID = '11111111-1111-1111-1111-111111111111';
const CODE = 'ABCDEFGHJK';

type MockFn = ReturnType<typeof vi.fn>;

describe('JoinCodePage', () => {
  let routeCode: string | null;
  let sessions: { join: MockFn };
  let router: Router;

  function createComponent(): JoinCodePage {
    const fixture = TestBed.createComponent(JoinCodePage);
    return fixture.componentInstance;
  }

  beforeEach(async () => {
    routeCode = null;
    sessions = {
      join: vi.fn().mockReturnValue(
        of({
          guestToken: 'guest-token',
          expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
          room: { id: ROOM_ID },
          code: CODE,
        }),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [JoinCodePage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeCode } } },
        },
        { provide: GuestSessionService, useValue: sessions },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  it('does not navigate with an invalid code', async () => {
    const component = createComponent();
    component.form.controls.code.setValue('CORTO');

    await component.onSubmit();

    expect(component.showCodeError()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to the direct join route with a valid code', async () => {
    const component = createComponent();
    component.form.controls.code.setValue(CODE);

    await component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith(['/join', CODE]);
  });

  it('enters the room automatically from a shared code link', async () => {
    routeCode = CODE;
    const fixture = TestBed.createComponent(JoinCodePage);

    fixture.detectChanges();

    await vi.waitFor(() => {
      expect(router.navigate).toHaveBeenCalledWith(['/room', ROOM_ID]);
    });
  });

  it('keeps the code and shows the error when the room does not exist', async () => {
    routeCode = CODE;
    sessions.join.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 404 })),
    );
    const fixture = TestBed.createComponent(JoinCodePage);

    fixture.detectChanges();

    await vi.waitFor(() => {
      expect(fixture.componentInstance.errorMessage()).toContain('No encontramos');
    });
    expect(fixture.componentInstance.form.controls.code.value).toBe(CODE);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
