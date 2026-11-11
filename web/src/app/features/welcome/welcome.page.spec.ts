import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { WelcomePage } from './welcome.page';

describe('WelcomePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WelcomePage],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the guest welcome content and a single entry action', () => {
    const fixture = TestBed.createComponent(WelcomePage);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('h1')?.textContent).toContain('Tu canción.');
    expect(compiled.querySelectorAll('a.button')).toHaveLength(1);
    expect(compiled.querySelector('a.button')?.textContent).toContain('Ingresar código');
    expect(compiled.querySelector('a.button')?.getAttribute('href')).toBe('/join');
  });

  it('lists the three steps in order', () => {
    const fixture = TestBed.createComponent(WelcomePage);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const steps = Array.from(compiled.querySelectorAll('.step__text')).map((step) =>
      step.textContent?.trim(),
    );

    expect(steps).toEqual([
      'Entra con el código',
      'Envía tu canción',
      'Disfruta el evento',
    ]);
  });
});
