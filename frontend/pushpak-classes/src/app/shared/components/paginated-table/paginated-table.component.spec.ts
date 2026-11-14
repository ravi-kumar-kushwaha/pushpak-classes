import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginatedTableComponent, TableColumn } from './paginated-table.component';

describe('PaginatedTableComponent', () => {
  let component: PaginatedTableComponent;
  let fixture: ComponentFixture<PaginatedTableComponent>;

  const columns: TableColumn[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'name', label: 'Name', sortable: true }
  ];

  const data = [
    { id: 1, name: 'Quiz 1' },
    { id: 2, name: 'Quiz 2' }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaginatedTableComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PaginatedTableComponent);
    component = fixture.componentInstance;
    component.columns = columns;
    component.data = data;
    component.totalCount = 20;
    component.pageSize = 10;
    component.pageNumber = 1;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should calculate total pages and page ranges correctly', () => {
    expect(component.totalPages).toBe(2);
    expect(component.startItem).toBe(1);
    expect(component.endItem).toBe(10);
  });

  it('should emit pageChange when navigating to valid page', () => {
    const pageSpy = vi.spyOn(component.pageChange, 'emit');
    component.onPageChange(2);
    expect(pageSpy).toHaveBeenCalledWith(2);
  });

  it('should toggle sort direction when sorting on same column', () => {
    const sortSpy = vi.spyOn(component.sortChange, 'emit');
    
    component.onSort('name');
    expect(sortSpy).toHaveBeenCalledWith({ field: 'name', direction: 'asc' });

    component.onSort('name');
    expect(sortSpy).toHaveBeenCalledWith({ field: 'name', direction: 'desc' });
  });

  it('should emit pageSizeChange when pageSize changes', () => {
    const sizeSpy = vi.spyOn(component.pageSizeChange, 'emit');
    component.onPageSizeChange(25);
    expect(sizeSpy).toHaveBeenCalledWith(25);
  });
});
