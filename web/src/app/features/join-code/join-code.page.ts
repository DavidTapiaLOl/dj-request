import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { mapHttpError } from '../../core/api-error';
import { GuestSessionService, normalizeCode } from '../../core/guest-session.service';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

const CODE_PATTERN = /^[A-HJ-NP-Z2-9]{10}$/;

@Component({
  selector: 'app-join-code',
  imports: [ReactiveFormsModule, RouterLink, BrandLogo],
  templateUrl: './join-code.page.html',
  styleUrl: './join-code.page.scss',
})
export class JoinCodePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sessions = inject(GuestSessionService);
  private readonly formBuilder = inject(FormBuilder);

  readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(CODE_PATTERN)]],
  });

  readonly submitted = signal(false);
  readonly joining = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private starting = false;

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    if (!code) return;

    const normalized = normalizeCode(code);
    this.form.controls.code.setValue(normalized);
    void this.enter(normalized);
  }

  onSubmit(): void {
    if (this.starting) return;

    this.submitted.set(true);
    this.errorMessage.set(null);

    const code = normalizeCode(this.form.controls.code.value);
    this.form.controls.code.setValue(code);

    if (this.form.invalid) return;

    if (this.route.snapshot.paramMap.get('code')) {
      void this.enter(code);
      return;
    }

    void this.router.navigate(['/join', code]);
  }

  showCodeError(): boolean {
    const control = this.form.controls.code;
    return control.invalid && (control.touched || this.submitted());
  }

  codeErrorMessage(): string {
    const control = this.form.controls.code;
    if (control.hasError('required')) return 'Introduce el código de la sala.';
    if (control.hasError('pattern')) {
      return 'El código debe tener 10 caracteres válidos (sin I, O, 0 ni 1).';
    }
    return 'El código no es válido.';
  }

  private async enter(code: string): Promise<void> {
    if (this.starting) return;

    this.starting = true;
    this.submitted.set(true);
    this.joining.set(true);
    this.errorMessage.set(null);

    try {
      const session = await firstValueFrom(this.sessions.join(code));
      await this.router.navigate(['/room', session.room.id]);
    } catch (error) {
      this.errorMessage.set(errorMessageFor(error));
    } finally {
      this.starting = false;
      this.joining.set(false);
    }
  }
}

function errorMessageFor(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const mapped = mapHttpError(error);
    if (mapped.kind === 'notFound') {
      return 'No encontramos una sala con ese código.';
    }
    if (mapped.kind === 'unavailable') {
      return 'La sala no está disponible para nuevas entradas en este momento.';
    }
    if (mapped.kind === 'rateLimited' && mapped.retryAfterSeconds !== null) {
      return `${mapped.message} Espera ${mapped.retryAfterSeconds} segundos.`;
    }
    return mapped.message;
  }
  return 'No pudimos entrar a la sala. Inténtalo de nuevo.';
}
