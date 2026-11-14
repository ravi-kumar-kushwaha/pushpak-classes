import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 2000;">
      @for (toast of toastService.toasts(); track toast.id) {
        <div 
          class="toast show shadow-lg border-0 mb-2" 
          [ngClass]="'bg-' + toast.type + ' text-white'" 
          role="alert" 
          aria-live="assertive" 
          aria-atomic="true">
          <div class="toast-header" [ngClass]="'bg-' + toast.type + ' text-white border-bottom-0'">
            <i class="bi me-2" [ngClass]="getIcon(toast.type)"></i>
            <strong class="me-auto">{{ toast.title }}</strong>
            <button 
              type="button" 
              class="btn-close btn-close-white" 
              (click)="toastService.remove(toast.id)" 
              aria-label="Close"></button>
          </div>
          <div class="toast-body pt-0">
            {{ toast.message }}
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast {
      min-width: 300px;
      max-width: 420px;
      animation: fadeIn 0.25s ease-in-out;
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-10px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class ToastContainerComponent {
  toastService = inject(ToastService);

  getIcon(type: string): string {
    switch (type) {
      case 'success': return 'bi-check-circle-fill';
      case 'danger': return 'bi-exclamation-triangle-fill';
      case 'warning': return 'bi-exclamation-circle-fill';
      case 'info': return 'bi-info-circle-fill';
      default: return 'bi-bell-fill';
    }
  }
}
