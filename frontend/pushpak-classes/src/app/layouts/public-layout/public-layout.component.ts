import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink, RouterOutlet],
  template: `
    <div class="min-vh-100 d-flex flex-column justify-content-between bg-light">
      <!-- Public Header -->
      <nav class="navbar navbar-expand-lg navbar-dark brand-gradient shadow-sm py-3">
        <div class="container">
          <a class="navbar-brand d-flex align-items-center gap-2 fw-bold fs-4" routerLink="/">
            <i class="bi bi-mortarboard-fill fs-3 text-warning"></i>
            <span>PushpakClasses</span>
          </a>
          <span class="navbar-text text-white-50 d-none d-md-inline">
            Timed Quiz Platform
          </span>
        </div>
      </nav>

      <!-- Main Content Container -->
      <div class="container my-auto py-5">
        <div class="row justify-content-center">
          <div class="col-12 col-sm-10 col-md-8 col-lg-5">
            <router-outlet></router-outlet>
          </div>
        </div>
      </div>

      <!-- Public Footer -->
      <footer class="bg-white border-top py-3 text-center text-muted small">
        <div class="container">
          <div>© 2026 PushpakClasses. All rights reserved. Server-authoritative timed examinations.</div>
        </div>
      </footer>
    </div>
  `
})
export class PublicLayoutComponent {}
