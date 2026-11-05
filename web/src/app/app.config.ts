import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideAppInitializer,
  inject,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { InstitutionService } from './core/institution';
import { PreloadAllModules, provideRouter, withComponentInputBinding, withPreloading } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(),
    provideAppInitializer(() => inject(InstitutionService).load()),
    provideBrowserGlobalErrorListeners(),
    // Lazy pages download in the background after the first screen, so later navigation is instant.
    provideRouter(routes, withComponentInputBinding(), withPreloading(PreloadAllModules)),
  ],
};
