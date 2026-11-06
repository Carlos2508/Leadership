import { CanDeactivateFn } from '@angular/router';

export interface PendingChanges {
  canLeave(): Promise<boolean>;
}

export const pendingChangesGuard: CanDeactivateFn<PendingChanges> = (component) =>
  component.canLeave();
