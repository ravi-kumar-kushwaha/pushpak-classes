import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TimeService } from '../../core/services/time.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="d-flex flex-column min-vh-100">
      <!-- Admin Top Navbar -->
      <nav class="navbar navbar-expand-lg navbar-dark bg-secondary bg-opacity-100 sticky-top shadow-sm px-3" style="background-color: #0f172a !important;">
        <div class="container-fluid">
          <button 
            class="btn btn-outline-light btn-sm me-3 d-lg-none" 
            type="button" 
            (click)="toggleSidebar()">
            <i class="bi bi-list fs-5"></i>
          </button>

          <a class="navbar-brand d-flex align-items-center gap-2 fw-bold" routerLink="/admin/dashboard">
            <i class="bi bi-shield-check text-warning fs-5"></i>
            <span>PushpakClasses</span>
            <span class="badge bg-danger ms-1 text-uppercase small" style="font-size: 0.65rem;">Admin</span>
          </a>

          <!-- Server UTC Clock Display -->
          <div class="ms-auto me-3 d-flex align-items-center text-white-50 small bg-dark px-3 py-1 rounded-pill">
            <i class="bi bi-clock-history text-warning me-2"></i>
            <span class="d-none d-sm-inline me-1">Server UTC:</span>
            <span class="text-white fw-semibold font-monospace">
              {{ timeService.currentTimeUtc() | date:'HH:mm:ss':'UTC' }}
            </span>
          </div>

          <!-- Admin Menu Dropdown -->
          <div class="dropdown">
            <button 
              class="btn btn-dark dropdown-toggle d-flex align-items-center gap-2 border-0" 
              type="button" 
              data-bs-toggle="dropdown" 
              aria-expanded="false">
              <div class="avatar-circle bg-danger text-white fw-bold">
                A
              </div>
              <span class="d-none d-md-inline text-white">{{ authService.currentUser()?.fullName }}</span>
            </button>
            <ul class="dropdown-menu dropdown-menu-end shadow border-0 mt-2">
              <li class="px-3 py-2 border-bottom">
                <div class="fw-bold">{{ authService.currentUser()?.fullName }}</div>
                <div class="text-muted small text-truncate" style="max-width: 200px;">
                  {{ authService.currentUser()?.email }}
                </div>
                <span class="badge bg-danger-subtle text-danger mt-1">Administrator</span>
              </li>
              <li>
                <a class="dropdown-item py-2" routerLink="/admin/dashboard">
                  <i class="bi bi-speedometer2 me-2"></i> Dashboard
                </a>
              </li>
              <li>
                <hr class="dropdown-divider">
              </li>
              <li>
                <button class="dropdown-item py-2 text-danger" (click)="onLogout()">
                  <i class="bi bi-box-arrow-right me-2"></i> Logout
                </button>
              </li>
            </ul>
          </div>
        </div>
      </nav>

      <!-- App Body Layout -->
      <div class="container-fluid flex-grow-1 p-0">
        <div class="row g-0 h-100">
          <!-- Admin Sidebar -->
          <aside 
            class="col-lg-2 bg-dark text-white py-3 px-2 sidebar" 
            [class.d-none]="!isSidebarOpen" 
            [class.d-lg-block]="true">
            <div class="list-group list-group-flush">
              <a 
                routerLink="/admin/dashboard" 
                routerLinkActive="active" 
                [routerLinkActiveOptions]="{ exact: true }"
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-speedometer2 fs-5 text-warning"></i>
                <span>Dashboard</span>
              </a>
              <a 
                routerLink="/admin/quizzes" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-journal-text fs-5 text-info"></i>
                <span>Quizzes</span>
              </a>
              <a 
                routerLink="/admin/categories" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-tags fs-5 text-success"></i>
                <span>Categories</span>
              </a>
              <a 
                routerLink="/admin/users" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-people fs-5 text-primary"></i>
                <span>Users</span>
              </a>
              <a 
                routerLink="/admin/attempts" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-clock-history fs-5 text-light"></i>
                <span>Attempts</span>
              </a>
              <a 
                routerLink="/admin/reattempts" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 text-white bg-transparent border-0">
                <i class="bi bi-arrow-repeat fs-5 text-warning"></i>
                <span>Reattempts</span>
              </a>
            </div>
          </aside>

          <!-- Main Content Area -->
          <main class="col-lg-10 p-4 bg-light flex-grow-1">
            <router-outlet></router-outlet>
          </main>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .avatar-circle {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.85rem;
    }
    .list-group-item.active {
      background-color: #1e293b !important;
      font-weight: 600;
      border-left: 3px solid #f59e0b !important;
    }
    .sidebar {
      min-height: calc(100vh - 56px);
      background-color: #0f172a !important;
    }
  `]
})
export class AdminLayoutComponent {
  authService = inject(AuthService);
  timeService = inject(TimeService);
  private router = inject(Router);

  isSidebarOpen = false;

  toggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  onLogout(): void {
    this.authService.logout();
  }
}
