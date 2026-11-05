import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

bootstrapApplication(App, appConfig).catch(() => {
  const root = document.querySelector('app-root')!;
  const message = document.createElement('p');
  message.textContent = 'No pudimos cargar la plataforma. Revisa tu conexión e inténtalo de nuevo.';
  message.setAttribute('role', 'alert');
  const retry = document.createElement('button');
  retry.textContent = 'Volver a intentar';
  retry.className = 'button';
  retry.onclick = () => window.location.reload();
  root.replaceChildren(message, retry);
  root.classList.add('bootstrap-error');
});
