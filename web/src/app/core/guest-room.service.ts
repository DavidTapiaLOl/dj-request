import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './api-config';
import { CreateSolicitudPayload, GuestRoomResponse, Solicitud, SolicitudPage } from './models';

@Injectable({ providedIn: 'root' })
export class GuestRoomService {
  private readonly http = inject(HttpClient);

  getRoom(roomId: string, guestToken: string): Observable<GuestRoomResponse> {
    return this.http.get<GuestRoomResponse>(`${API_BASE}/guest/rooms/${roomId}`, {
      headers: guestHeaders(guestToken),
    });
  }

  listRequests(roomId: string, guestToken: string, page: number, pageSize = 20): Observable<SolicitudPage> {
    return this.http.get<SolicitudPage>(`${API_BASE}/guest/rooms/${roomId}/requests`, {
      headers: guestHeaders(guestToken),
      params: { page, pageSize },
    });
  }

  createRequest(roomId: string, guestToken: string, payload: CreateSolicitudPayload): Observable<Solicitud> {
    return this.http.post<Solicitud>(`${API_BASE}/guest/rooms/${roomId}/requests`, payload, {
      headers: guestHeaders(guestToken),
    });
  }
}

function guestHeaders(guestToken: string): HttpHeaders {
  return new HttpHeaders({ 'X-Guest-Token': guestToken });
}
