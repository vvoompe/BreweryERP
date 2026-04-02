import { inject }       from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService }  from './auth.service';

/** Redirects unauthenticated users to /login */
export const authGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }
  return router.createUrlTree(['/login']);
};

/** Redirects already-logged-in users away from /login */
export const guestGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return true;
  }
  return router.createUrlTree(['/dashboard']);
};
