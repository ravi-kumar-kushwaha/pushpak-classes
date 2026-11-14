import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { PublicLayoutComponent } from './layouts/public-layout/public-layout.component';
import { UserLayoutComponent } from './layouts/user-layout/user-layout.component';
import { AdminLayoutComponent } from './layouts/admin-layout/admin-layout.component';

export const routes: Routes = [
  // Public / Authentication routes
  {
    path: 'auth',
    component: PublicLayoutComponent,
    children: [
      {
        path: 'login',
        loadComponent: () => import('./pages/auth/login/login.component').then(m => m.LoginComponent)
      },
      {
        path: 'signup',
        loadComponent: () => import('./pages/auth/signup/signup.component').then(m => m.SignupComponent)
      },
      {
        path: 'forgot-password',
        loadComponent: () => import('./pages/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
      },
      {
        path: 'reset-password',
        loadComponent: () => import('./pages/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
      },
      {
        path: 'verify-email',
        loadComponent: () => import('./pages/auth/verify-email/verify-email.component').then(m => m.VerifyEmailComponent)
      },
      {
        path: '',
        redirectTo: 'login',
        pathMatch: 'full'
      }
    ]
  },

  // Student / User portal routes
  {
    path: 'user',
    component: UserLayoutComponent,
    canActivate: [authGuard, roleGuard(['User'])],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./pages/user/dashboard/user-dashboard.component').then(m => m.UserDashboardComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./pages/user/profile/profile.component').then(m => m.ProfileComponent)
      },
      {
        path: 'quizzes',
        loadComponent: () => import('./pages/user/quizzes/quiz-list.component').then(m => m.QuizListComponent)
      },
      {
        path: 'attempts',
        loadComponent: () => import('./pages/user/attempts/attempt-history.component').then(m => m.AttemptHistoryComponent)
      },
      {
        path: 'attempts/:id/play',
        loadComponent: () => import('./pages/user/quizzes/quiz-play/quiz-play.component').then(m => m.QuizPlayComponent)
      },
      {
        path: 'attempts/:id/review',
        loadComponent: () => import('./pages/user/attempts/attempt-review/attempt-review.component').then(m => m.AttemptReviewComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },

  // Administrator portal routes
  {
    path: 'admin',
    component: AdminLayoutComponent,
    canActivate: [authGuard, roleGuard(['Admin'])],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./pages/admin/dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent)
      },
      {
        path: 'quizzes',
        loadComponent: () => import('./pages/admin/quizzes/admin-quiz-list.component').then(m => m.AdminQuizListComponent)
      },
      {
        path: 'quizzes/new',
        loadComponent: () => import('./pages/admin/quizzes/admin-quiz-edit/admin-quiz-edit.component').then(m => m.AdminQuizEditComponent)
      },
      {
        path: 'quizzes/:id/edit',
        loadComponent: () => import('./pages/admin/quizzes/admin-quiz-edit/admin-quiz-edit.component').then(m => m.AdminQuizEditComponent)
      },
      {
        path: 'categories',
        loadComponent: () => import('./pages/admin/categories/admin-category-list.component').then(m => m.AdminCategoryListComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./pages/admin/users/admin-user-list.component').then(m => m.AdminUserListComponent)
      },
      {
        path: 'attempts',
        loadComponent: () => import('./pages/admin/attempts/admin-attempt-list.component').then(m => m.AdminAttemptListComponent)
      },
      {
        path: 'attempts/:id',
        loadComponent: () => import('./pages/admin/attempts/admin-attempt-detail/admin-attempt-detail.component').then(m => m.AdminAttemptDetailComponent)
      },
      {
        path: 'reattempts',
        loadComponent: () => import('./pages/admin/reattempts/admin-reattempt-list.component').then(m => m.AdminReattemptListComponent)
      },
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      }
    ]
  },

  // Root redirect to auth login
  {
    path: '',
    redirectTo: '/auth/login',
    pathMatch: 'full'
  },

  // Direct top-level redirects for email links
  {
    path: 'verify-email',
    redirectTo: '/auth/verify-email'
  },
  {
    path: 'reset-password',
    redirectTo: '/auth/reset-password'
  },

  // Wildcard fallback
  {
    path: '**',
    redirectTo: '/auth/login'
  }
];
