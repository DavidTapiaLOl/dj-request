export function roomStateLabel(state: string): string {
  switch (state) {
    case 'abierta':
      return 'Abierta';
    case 'pausada':
      return 'Pausada';
    case 'cerrada':
      return 'Cerrada';
    default:
      return state;
  }
}

export function solicitudEstadoLabel(estado: string): string {
  switch (estado) {
    case 'pendiente':
      return 'Pendiente';
    case 'aceptada':
      return 'Aceptada';
    case 'reproducida':
      return 'Reproducida';
    case 'rechazada':
      return 'Rechazada';
    default:
      return estado;
  }
}

export function formatSchedule(startsAt: string, endsAt: string, timezone: string): string {
  try {
    const formatter = new Intl.DateTimeFormat('es-MX', {
      timeZone: timezone,
      dateStyle: 'medium',
      timeStyle: 'short',
    });
    return `${formatter.format(new Date(startsAt))} — ${formatter.format(new Date(endsAt))}`;
  } catch {
    return `${new Date(startsAt).toLocaleString()} — ${new Date(endsAt).toLocaleString()}`;
  }
}
