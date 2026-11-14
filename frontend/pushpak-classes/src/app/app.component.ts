import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './shared/components/toast-container/toast-container.component';
import { LoaderComponent } from './shared/components/loader/loader.component';
import { ConfirmDialogComponent } from './shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastContainerComponent, LoaderComponent, ConfirmDialogComponent],
  template: `
    <app-loader></app-loader>
    <app-toast-container></app-toast-container>
    <app-confirm-dialog></app-confirm-dialog>
    <router-outlet></router-outlet>
  `,
  styleUrl: './app.component.scss'
})
export class App {
  title = 'PushpakClasses';
}
