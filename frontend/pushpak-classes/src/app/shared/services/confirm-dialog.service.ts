import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  isDanger?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmDialogService {
  isOpen = signal<boolean>(false);
  options = signal<ConfirmDialogOptions>({
    title: 'Confirm Action',
    message: 'Are you sure you want to proceed?',
    confirmText: 'Confirm',
    cancelText: 'Cancel',
    isDanger: false
  });

  private resolvePromise?: (value: boolean) => void;

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    this.options.set({
      confirmText: 'Confirm',
      cancelText: 'Cancel',
      isDanger: false,
      ...options
    });
    this.isOpen.set(true);

    return new Promise<boolean>((resolve) => {
      this.resolvePromise = resolve;
    });
  }

  handleConfirm(): void {
    this.isOpen.set(false);
    if (this.resolvePromise) {
      this.resolvePromise(true);
      this.resolvePromise = undefined;
    }
  }

  handleCancel(): void {
    this.isOpen.set(false);
    if (this.resolvePromise) {
      this.resolvePromise(false);
      this.resolvePromise = undefined;
    }
  }
}
