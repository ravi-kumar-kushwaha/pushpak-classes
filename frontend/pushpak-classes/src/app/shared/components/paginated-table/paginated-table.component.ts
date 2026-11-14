import { Component, Input, Output, EventEmitter, TemplateRef, ContentChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

export interface TableColumn {
  key: string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'center' | 'right';
  width?: string;
}

@Component({
  selector: 'app-paginated-table',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card shadow-sm border-0">
      <div class="card-body p-3">
        <!-- Top Toolbar: Search & PageSize -->
        <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-3">
          <div class="input-group" style="max-width: 350px;">
            <span class="input-group-text bg-white border-end-0">
              <i class="bi bi-search text-muted"></i>
            </span>
            <input 
              type="text" 
              class="form-control border-start-0 ps-0" 
              [placeholder]="searchPlaceholder" 
              [ngModel]="searchTerm" 
              (ngModelChange)="onSearchInput($event)" />
            @if (searchTerm) {
              <button class="btn btn-outline-secondary border-start-0" type="button" (click)="clearSearch()">
                <i class="bi bi-x-lg"></i>
              </button>
            }
          </div>

          <div class="d-flex align-items-center gap-2">
            <label class="text-muted small text-nowrap">Items per page:</label>
            <select 
              class="form-select form-select-sm w-auto" 
              [ngModel]="pageSize" 
              (ngModelChange)="onPageSizeChange($event)">
              <option [value]="10">10</option>
              <option [value]="25">25</option>
              <option [value]="50">50</option>
              <option [value]="100">100</option>
            </select>
          </div>
        </div>

        <!-- Table Responsive Container -->
        <div class="table-responsive position-relative">
          @if (isLoading) {
            <div class="position-absolute top-0 start-0 w-100 h-100 bg-white bg-opacity-75 d-flex align-items-center justify-content-center" style="z-index: 10;">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          }

          <table class="table table-hover align-middle mb-0">
            <thead class="table-light">
              <tr>
                @for (col of columns; track col.key) {
                  <th 
                    [style.width]="col.width || 'auto'" 
                    [ngClass]="'text-' + (col.align || 'start')"
                    [class.cursor-pointer]="col.sortable"
                    (click)="col.sortable && onSort(col.key)">
                    <div class="d-inline-flex align-items-center gap-1">
                      <span>{{ col.label }}</span>
                      @if (col.sortable) {
                        @if (sortField === col.key) {
                          <i class="bi" [ngClass]="sortDirection === 'asc' ? 'bi-sort-up text-primary' : 'bi-sort-down text-primary'"></i>
                        } @else {
                          <i class="bi bi-arrow-down-up text-muted opacity-50 small"></i>
                        }
                      }
                    </div>
                  </th>
                }
                @if (actionTemplate) {
                  <th class="text-end" style="width: 120px;">Actions</th>
                }
              </tr>
            </thead>
            <tbody>
              @if (data.length === 0 && !isLoading) {
                <tr>
                  <td [attr.colspan]="columns.length + (actionTemplate ? 1 : 0)" class="text-center py-5 text-muted">
                    <i class="bi bi-inbox fs-1 d-block mb-2 text-secondary"></i>
                    {{ emptyMessage }}
                  </td>
                </tr>
              } @else {
                @for (row of data; track row.id || $index) {
                  <tr>
                    @for (col of columns; track col.key) {
                      <td [ngClass]="'text-' + (col.align || 'start')">
                        {{ row[col.key] }}
                      </td>
                    }
                    @if (actionTemplate) {
                      <td class="text-end">
                        <ng-container *ngTemplateOutlet="actionTemplate; context: { $implicit: row }"></ng-container>
                      </td>
                    }
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>

        <!-- Pagination & Summary Footer -->
        <div class="d-flex flex-column flex-md-row justify-content-between align-items-center gap-3 pt-3 mt-2 border-top">
          <div class="text-muted small">
            @if (totalCount > 0) {
              Showing {{ startItem }} to {{ endItem }} of {{ totalCount }} results
            } @else {
              No results found
            }
          </div>

          @if (totalPages > 1) {
            <nav aria-label="Table navigation">
              <ul class="pagination pagination-sm mb-0">
                <li class="page-item" [class.disabled]="pageNumber <= 1">
                  <button class="page-link" (click)="onPageChange(pageNumber - 1)" [disabled]="pageNumber <= 1">
                    <i class="bi bi-chevron-left"></i>
                  </button>
                </li>
                
                @for (p of displayedPages; track p) {
                  <li class="page-item" [class.active]="p === pageNumber">
                    <button class="page-link" (click)="onPageChange(p)">{{ p }}</button>
                  </li>
                }

                <li class="page-item" [class.disabled]="pageNumber >= totalPages">
                  <button class="page-link" (click)="onPageChange(pageNumber + 1)" [disabled]="pageNumber >= totalPages">
                    <i class="bi bi-chevron-right"></i>
                  </button>
                </li>
              </ul>
            </nav>
          }
        </div>
      </div>
    </div>
  `
})
export class PaginatedTableComponent {
  @Input() columns: TableColumn[] = [];
  @Input() data: any[] = [];
  @Input() totalCount: number = 0;
  @Input() pageNumber: number = 1;
  @Input() pageSize: number = 10;
  @Input() isLoading: boolean = false;
  @Input() searchPlaceholder: string = 'Search...';
  @Input() emptyMessage: string = 'No records found.';
  @Input() sortField: string = '';
  @Input() sortDirection: 'asc' | 'desc' = 'asc';

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() sortChange = new EventEmitter<{ field: string; direction: 'asc' | 'desc' }>();
  @Output() searchChange = new EventEmitter<string>();

  @Input() actionTemplate?: TemplateRef<any>;

  searchTerm: string = '';
  private searchSubject = new Subject<string>();

  constructor() {
    this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged()
    ).subscribe(term => {
      this.searchChange.emit(term);
    });
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize) || 1;
  }

  get startItem(): number {
    return this.totalCount === 0 ? 0 : (this.pageNumber - 1) * this.pageSize + 1;
  }

  get endItem(): number {
    return Math.min(this.pageNumber * this.pageSize, this.totalCount);
  }

  get displayedPages(): number[] {
    const pages: number[] = [];
    const total = this.totalPages;
    const current = this.pageNumber;
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

  onSearchInput(term: string): void {
    this.searchTerm = term;
    this.searchSubject.next(term);
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.searchSubject.next('');
  }

  onPageSizeChange(newSize: any): void {
    const size = Number(newSize);
    this.pageSize = size;
    this.pageSizeChange.emit(size);
  }

  onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages && page !== this.pageNumber) {
      this.pageChange.emit(page);
    }
  }

  onSort(field: string): void {
    let direction: 'asc' | 'desc' = 'asc';
    if (this.sortField === field) {
      direction = this.sortDirection === 'asc' ? 'desc' : 'asc';
    }
    this.sortField = field;
    this.sortDirection = direction;
    this.sortChange.emit({ field, direction });
  }
}
