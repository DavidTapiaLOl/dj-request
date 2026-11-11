import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/welcome/welcome.page').then((m) => m.WelcomePage),
    title: 'DJ Requests — Inicio',
  },
  {
    path: 'join',
    loadComponent: () => import('./features/join-code/join-code.page').then((m) => m.JoinCodePage),
    title: 'DJ Requests — Entrar',
  },
  {
    path: 'join/:code',
    loadComponent: () => import('./features/join-code/join-code.page').then((m) => m.JoinCodePage),
    title: 'DJ Requests — Entrar',
  },
  {
    path: 'room/:roomId/dj',
    loadComponent: () =>
      import('./features/dj-profile/dj-profile.page').then((m) => m.DjProfilePage),
    title: 'DJ Requests — Perfil del DJ',
  },
  {
    path: 'room/:roomId',
    loadComponent: () => import('./features/room/room.page').then((m) => m.RoomPage),
    title: 'DJ Requests — Sala',
  },
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found.page').then((m) => m.NotFoundPage),
    title: 'DJ Requests — Página no encontrada',
  },
];
