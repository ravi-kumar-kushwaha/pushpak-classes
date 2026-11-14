import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoadingService } from '../../../core/services/loading.service';

@Component({
  selector: 'app-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (loadingService.isLoading()) {
      <div class="progress position-fixed top-0 start-0 w-100" style="height: 3px; z-index: 9999; border-radius: 0;">
        <div class="progress-bar progress-bar-striped progress-bar-animated bg-primary w-100" role="progressbar"></div>
      </div>
    }
  `
})
export class LoaderComponent {
  loadingService = inject(LoadingService);
}
