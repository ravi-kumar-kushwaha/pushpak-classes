import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (dialogService.isOpen()) {
      <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0, 0, 0, 0.5); z-index: 1080;">
        <div class="modal-dialog modal-dialog-centered">
          <div class="modal-content shadow border-0">
            <div class="modal-header">
              <h5 class="modal-title d-flex align-items-center">
                @if (dialogService.options().isDanger) {
                  <i class="bi bi-exclamation-triangle-fill text-danger me-2"></i>
                } @else {
                  <i class="bi bi-question-circle-fill text-primary me-2"></i>
                }
                {{ dialogService.options().title }}
              </h5>
              <button type="button" class="btn-close" (click)="dialogService.handleCancel()"></button>
            </div>
            <div class="modal-body">
              <p class="mb-0 text-secondary">{{ dialogService.options().message }}</p>
            </div>
            <div class="modal-footer">
              <button 
                type="button" 
                class="btn btn-outline-secondary" 
                (click)="dialogService.handleCancel()">
                {{ dialogService.options().cancelText }}
              </button>
              <button 
                type="button" 
                class="btn" 
                [ngClass]="dialogService.options().isDanger ? 'btn-danger' : 'btn-primary'" 
                (click)="dialogService.handleConfirm()">
                {{ dialogService.options().confirmText }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `
})
export class ConfirmDialogComponent {
  dialogService = inject(ConfirmDialogService);
}
