import { Injectable } from '@angular/core';
import Swal, { SweetAlertIcon, SweetAlertOptions } from 'sweetalert2';

export interface ConfirmOptions {
  title: string;
  text?: string;
  confirmText: string;
  cancelText?: string;
  /** Destructive or irreversible: red confirm button and warning icon. */
  danger?: boolean;
}

export interface ReasonOptions extends ConfirmOptions {
  label: string;
  minLength?: number;
}

/**
 * Feedback for user actions (SweetAlert2): toasts for completed actions, modals for failures and confirmations.
 * Styled by the institution theme (CSS variables from config) and the platform buttons; texts in Spanish.
 * When a native modal <dialog> is open the alert is mounted inside it, so it stays in the top layer and usable.
 */
@Injectable({ providedIn: 'root' })
export class Alerts {
  private base(): SweetAlertOptions {
    return {
      target: (document.querySelector('dialog[open]') as HTMLElement | null) ?? document.body,
      buttonsStyling: false,
      reverseButtons: true,
      focusCancel: false,
      returnFocus: true,
      customClass: {
        container: 'sp-alert-container',
        popup: 'sp-alert',
        title: 'sp-alert-title',
        htmlContainer: 'sp-alert-text',
        actions: 'sp-alert-actions',
        confirmButton: 'button',
        cancelButton: 'button outline',
        input: 'sp-alert-input',
        inputLabel: 'sp-alert-label',
        validationMessage: 'sp-alert-validation',
        icon: 'sp-alert-icon',
      },
      showClass: { popup: 'sp-alert-in' },
      hideClass: { popup: 'sp-alert-out' },
    };
  }

  /** Short confirmation of a completed action; does not take focus away from the page. */
  success(title: string, text?: string): void {
    this.toast('success', title, text);
  }

  info(title: string, text?: string): void {
    this.toast('info', title, text);
  }

  private toast(icon: SweetAlertIcon, title: string, text?: string): void {
    void Swal.fire({
      ...this.base(),
      // Toasts always live on the page: a dialog that closes right after the action must not take the toast with it.
      target: document.body,
      toast: true,
      position: 'top-end',
      icon,
      title,
      text,
      showConfirmButton: false,
      showCloseButton: true,
      closeButtonAriaLabel: 'Cerrar aviso',
      timer: 4200,
      timerProgressBar: true,
      customClass: { ...this.base().customClass, popup: 'sp-alert sp-toast', container: 'sp-alert-container sp-toast-container' },
      didOpen: (popup) => {
        popup.setAttribute('role', 'status');
        popup.setAttribute('aria-live', 'polite');
        popup.addEventListener('mouseenter', Swal.stopTimer);
        popup.addEventListener('mouseleave', Swal.resumeTimer);
      },
    });
  }

  /** A completed action with a follow-up (e.g. open the receipt). Resolves true when the follow-up is chosen. */
  async done(title: string, text: string, followUp: string, dismiss = 'Listo'): Promise<boolean> {
    const result = await Swal.fire({
      ...this.base(),
      icon: 'success',
      title,
      text,
      showCancelButton: true,
      confirmButtonText: followUp,
      cancelButtonText: dismiss,
    });
    return result.isConfirmed;
  }

  /** An action could not be completed: the reason from the server, with an explicit acknowledgement. */
  error(title: string, text?: string): Promise<void> {
    return Swal.fire({
      ...this.base(),
      icon: 'error',
      title,
      text,
      confirmButtonText: 'Entendido',
    }).then(() => undefined);
  }

  /** Ask before an irreversible or sensitive action. Resolves true only when the user confirms. */
  async confirm(options: ConfirmOptions): Promise<boolean> {
    const result = await Swal.fire({
      ...this.base(),
      icon: options.danger ? 'warning' : 'question',
      title: options.title,
      text: options.text,
      showCancelButton: true,
      confirmButtonText: options.confirmText,
      cancelButtonText: options.cancelText ?? 'Cancelar',
      customClass: { ...this.base().customClass, confirmButton: options.danger ? 'button danger' : 'button' },
    });
    return result.isConfirmed;
  }

  /** Confirmation that requires a written reason (kept in the audit log). Resolves the reason or null. */
  async reason(options: ReasonOptions): Promise<string | null> {
    const min = options.minLength ?? 8;
    const result = await Swal.fire({
      ...this.base(),
      icon: options.danger ? 'warning' : 'question',
      title: options.title,
      text: options.text,
      input: 'textarea',
      inputLabel: options.label,
      inputAttributes: { maxlength: '500', 'aria-label': options.label },
      showCancelButton: true,
      confirmButtonText: options.confirmText,
      cancelButtonText: options.cancelText ?? 'Cancelar',
      customClass: { ...this.base().customClass, confirmButton: options.danger ? 'button danger' : 'button' },
      inputValidator: (value) => ((value ?? '').trim().length < min ? `Escribe un motivo de al menos ${min} caracteres.` : null),
    });
    return result.isConfirmed ? String(result.value).trim() : null;
  }
}
