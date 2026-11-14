import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TimeService } from '../../core/services/time.service';

@Component({
  selector: 'app-user-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="d-flex flex-column min-vh-100">
      <!-- Top Navigation Bar -->
      <nav class="navbar navbar-expand-lg navbar-dark bg-dark sticky-top shadow-sm px-3">
        <div class="container-fluid">
          <button 
            class="btn btn-outline-light btn-sm me-3 d-lg-none" 
            type="button" 
            (click)="toggleSidebar()">
            <i class="bi bi-list fs-5"></i>
          </button>

          <a class="navbar-brand d-flex align-items-center gap-2 fw-bold" routerLink="/user/dashboard">
            <i class="bi bi-mortarboard-fill text-warning"></i>
            <span>PushpakClasses</span>
          </a>

          <!-- Server Authoritative UTC Clock Display -->
          <div class="ms-auto me-3 d-flex align-items-center text-white-50 small bg-secondary bg-opacity-25 px-3 py-1 rounded-pill">
            <i class="bi bi-clock-history text-info me-2"></i>
            <span class="d-none d-sm-inline me-1">Server UTC:</span>
            <span class="text-white fw-semibold font-monospace">
              {{ timeService.currentTimeUtc() | date:'HH:mm:ss':'UTC' }}
            </span>
          </div>

          <!-- User Menu Dropdown -->
          <div class="dropdown">
            <button 
              class="btn btn-dark dropdown-toggle d-flex align-items-center gap-2 border-0" 
              type="button" 
              data-bs-toggle="dropdown" 
              aria-expanded="false">
              <div class="avatar-circle overflow-hidden bg-primary text-white fw-bold">
                @if (authService.currentUser()?.profileImageUrl) {
                  <img 
                    [src]="getAvatarUrl(authService.currentUser()?.profileImageUrl)" 
                    alt="Avatar" 
                    class="w-100 h-100 object-fit-cover" 
                    (error)="onAvatarError($event)" />
                } @else {
                  {{ userInitials }}
                }
              </div>
              <span class="d-none d-md-inline text-white">{{ authService.currentUser()?.fullName }}</span>
            </button>
            <ul class="dropdown-menu dropdown-menu-end shadow border-0 mt-2">
              <li class="px-3 py-2 border-bottom">
                <div class="fw-bold">{{ authService.currentUser()?.fullName }}</div>
                <div class="text-muted small text-truncate" style="max-width: 200px;">
                  {{ authService.currentUser()?.email }}
                </div>
                <span class="badge bg-primary-subtle text-primary mt-1">Student</span>
              </li>
              <li>
                <a class="dropdown-item py-2" routerLink="/user/dashboard">
                  <i class="bi bi-speedometer2 me-2"></i> Dashboard
                </a>
              </li>
              <li>
                <a class="dropdown-item py-2" routerLink="/user/profile">
                  <i class="bi bi-person me-2"></i> My Profile
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
          <!-- Sidebar -->
          <aside 
            class="col-lg-2 bg-white border-end py-3 px-2 sidebar" 
            [class.d-none]="!isSidebarOpen" 
            [class.d-lg-block]="true">
            <div class="list-group list-group-flush">
              <a 
                routerLink="/user/dashboard" 
                routerLinkActive="active" 
                [routerLinkActiveOptions]="{ exact: true }"
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 border-0">
                <i class="bi bi-speedometer2 fs-5"></i>
                <span>Dashboard</span>
              </a>
              <a 
                routerLink="/user/quizzes" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 border-0">
                <i class="bi bi-journal-check fs-5"></i>
                <span>Available Quizzes</span>
              </a>
              <a 
                routerLink="/user/attempts" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 border-0">
                <i class="bi bi-clock-history fs-5"></i>
                <span>My Attempts</span>
              </a>
              <a 
                routerLink="/user/profile" 
                routerLinkActive="active" 
                class="list-group-item list-group-item-action d-flex align-items-center gap-2 rounded mb-1 py-2 border-0">
                <i class="bi bi-person fs-5"></i>
                <span>My Profile</span>
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
      background-color: #2563eb;
      color: #fff;
    }
    .sidebar {
      min-height: calc(100vh - 56px);
    }
  `]
})
export class UserLayoutComponent {
  authService = inject(AuthService);
  timeService = inject(TimeService);
  private router = inject(Router);

  isSidebarOpen = false;

  get userInitials(): string {
    const name = this.authService.currentUser()?.fullName || 'User';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }

  getAvatarUrl(url: string | null | undefined): string | null {
    if (!url) return null;
    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) return url;
    return `http://localhost:5139${url.startsWith('/') ? '' : '/'}${url}`;
  }

  onAvatarError(event: Event): void {
    const target = event.target as HTMLImageElement;
    target.onerror = null;
    target.src = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="%23ffffff"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>';
  }

  toggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  onLogout(): void {
    this.authService.logout();
  }
}
