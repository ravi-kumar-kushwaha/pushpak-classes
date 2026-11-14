import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-paginator',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="d-flex flex-column flex-md-row justify-content-between align-items-center gap-3 py-3 px-2 border-top">
      <!-- Showing items summary -->
      <div class="text-muted small">
        @if (totalCount > 0) {
          Showing <span class="fw-semibold text-dark">{{ startItem }}</span> to 
          <span class="fw-semibold text-dark">{{ endItem }}</span> of 
          <span class="fw-semibold text-dark">{{ totalCount }}</span> results
        } @else {
          <span class="text-muted">No items found</span>
        }
      </div>

      <!-- Controls: PageSize selector & Navigation -->
      <div class="d-flex flex-wrap align-items-center gap-3">
        <!-- Page size dropdown -->
        <div class="d-flex align-items-center gap-2">
          <label class="text-muted small text-nowrap mb-0" for="paginatorPageSize">Per page:</label>
          <select 
            id="paginatorPageSize"
            class="form-select form-select-sm w-auto" 
            [ngModel]="pageSize" 
            (ngModelChange)="onPageSizeChange($event)"
            [disabled]="disabled">
            @for (opt of pageSizeOptions; track opt) {
              <option [value]="opt">{{ opt }}</option>
            }
          </select>
        </div>

        <!-- First / Prev / Page Numbers / Next / Last -->
        <nav aria-label="Pagination Navigation">
          <ul class="pagination pagination-sm mb-0">
            <!-- First -->
            <li class="page-item" [class.disabled]="disabled || page <= 1">
              <button 
                type="button"
                class="page-link" 
                title="First Page"
                (click)="goToPage(1)" 
                [disabled]="disabled || page <= 1">
                <i class="bi bi-chevron-double-left"></i>
              </button>
            </li>

            <!-- Prev -->
            <li class="page-item" [class.disabled]="disabled || page <= 1">
              <button 
                type="button"
                class="page-link" 
                title="Previous Page"
                (click)="goToPage(page - 1)" 
                [disabled]="disabled || page <= 1">
                <i class="bi bi-chevron-left"></i>
              </button>
            </li>

            <!-- Page Numbers -->
            @for (p of displayedPages; track p) {
              <li class="page-item" [class.active]="p === page" [class.disabled]="disabled">
                <button 
                  type="button"
                  class="page-link" 
                  (click)="goToPage(p)"
                  [disabled]="disabled">
                  {{ p }}
                </button>
              </li>
            }

            <!-- Next -->
            <li class="page-item" [class.disabled]="disabled || page >= totalPages">
              <button 
                type="button"
                class="page-link" 
                title="Next Page"
                (click)="goToPage(page + 1)" 
                [disabled]="disabled || page >= totalPages">
                <i class="bi bi-chevron-right"></i>
              </button>
            </li>

            <!-- Last -->
            <li class="page-item" [class.disabled]="disabled || page >= totalPages">
              <button 
                type="button"
                class="page-link" 
                title="Last Page"
                (click)="goToPage(totalPages)" 
                [disabled]="disabled || page >= totalPages">
                <i class="bi bi-chevron-double-right"></i>
              </button>
            </li>
          </ul>
        </nav>
      </div>
    </div>
  `
})
export class PaginatorComponent {
  @Input() page: number = 1;
  @Input() pageSize: number = 10;
  @Input() totalCount: number = 0;
  @Input() pageSizeOptions: number[] = [10, 25, 50];
  @Input() disabled: boolean = false;

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize) || 1;
  }

  get startItem(): number {
    return this.totalCount === 0 ? 0 : (this.page - 1) * this.pageSize + 1;
  }

  get endItem(): number {
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  get displayedPages(): number[] {
    const pages: number[] = [];
    const total = this.totalPages;
    const current = this.page;
    const maxVisible = 5;

    let start = Math.max(1, current - 2);
    let end = Math.min(total, start + maxVisible - 1);

    if (end - start < maxVisible - 1) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  goToPage(targetPage: number): void {
    if (this.disabled) return;
    if (targetPage >= 1 && targetPage <= this.totalPages && targetPage !== this.page) {
      this.pageChange.emit(targetPage);
    }
  }

  onPageSizeChange(newSize: any): void {
    const size = Number(newSize);
    if (!isNaN(size) && size > 0 && size !== this.pageSize) {
      this.pageSizeChange.emit(size);
    }
  }
}
