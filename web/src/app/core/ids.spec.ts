import { afterEach, describe, expect, it, vi } from 'vitest';
import { newIdempotencyKey } from './ids';

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('newIdempotencyKey', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('usa crypto.randomUUID cuando está disponible', () => {
    vi.stubGlobal('crypto', { randomUUID: () => '123e4567-e89b-42d3-a456-426614174000' });

    expect(newIdempotencyKey()).toBe('123e4567-e89b-42d3-a456-426614174000');
  });

  it('genera un UUID v4 con crypto.getRandomValues si randomUUID no existe', () => {
    vi.stubGlobal('crypto', {
      getRandomValues: (bytes: Uint8Array) => {
        bytes.fill(0xab);
        return bytes;
      },
    });

    expect(newIdempotencyKey()).toMatch(UUID_V4);
  });

  it('falla de forma explícita si no hay fuente criptográfica', () => {
    vi.stubGlobal('crypto', {});

    expect(() => newIdempotencyKey()).toThrow(/criptográfica/);
  });
});
