import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './api-config';
import { Genero } from './models';

@Injectable({ providedIn: 'root' })
export class GenreService {
  private readonly http = inject(HttpClient);

  list(): Observable<Genero[]> {
    return this.http.get<Genero[]>(`${API_BASE}/generos`);
  }
}
