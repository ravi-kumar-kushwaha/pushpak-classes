import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'danger' | 'warning' | 'info';
  title?: string;
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  toasts = signal<Toast[]>([]);

  showSuccess(message: string, title: string = 'Success', duration: number = 5000): void {
    this.add({ id: this.generateId(), message, title, type: 'success', duration });
  }

  showError(message: string, title: string = 'Error', duration: number = 7000): void {
    this.add({ id: this.generateId(), message, title, type: 'danger', duration });
  }

  showWarning(message: string, title: string = 'Warning', duration: number = 6000): void {
    this.add({ id: this.generateId(), message, title, type: 'warning', duration });
  }

  showInfo(message: string, title: string = 'Information', duration: number = 5000): void {
    this.add({ id: this.generateId(), message, title, type: 'info', duration });
  }

  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }

  private add(toast: Toast): void {
    this.toasts.update(list => [...list, toast]);
    if (toast.duration && toast.duration > 0) {
      setTimeout(() => {
        this.remove(toast.id);
      }, toast.duration);
    }
  }

  private generateId(): string {
    return Math.random().toString(36).substring(2, 9);
  }
}
