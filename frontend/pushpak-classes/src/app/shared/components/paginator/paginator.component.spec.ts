import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginatorComponent } from './paginator.component';

describe('PaginatorComponent', () => {
  let component: PaginatorComponent;
  let fixture: ComponentFixture<PaginatorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaginatorComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PaginatorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should compute totalPages and range correctly', () => {
    component.totalCount = 45;
    component.pageSize = 10;
    component.page = 2;

    expect(component.totalPages).toBe(5);
    expect(component.startItem).toBe(11);
    expect(component.endItem).toBe(20);
  });

  it('should emit pageChange when navigating forward, backward, first, and last', () => {
    component.totalCount = 100;
    component.pageSize = 10;
    component.page = 3;

    const pageSpy = vi.fn();
    component.pageChange.subscribe(pageSpy);

    component.goToPage(4);
    expect(pageSpy).toHaveBeenCalledWith(4);

    component.goToPage(1);
    expect(pageSpy).toHaveBeenCalledWith(1);

    component.goToPage(10);
    expect(pageSpy).toHaveBeenCalledWith(10);
  });

  it('should not emit pageChange when navigating to the current page or out of bounds', () => {
    component.totalCount = 50;
    component.pageSize = 10;
    component.page = 1;

    const pageSpy = vi.fn();
    component.pageChange.subscribe(pageSpy);

    component.goToPage(1);
    expect(pageSpy).not.toHaveBeenCalled();

    component.goToPage(0);
    expect(pageSpy).not.toHaveBeenCalled();

    component.goToPage(6);
    expect(pageSpy).not.toHaveBeenCalled();
  });

  it('should emit pageSizeChange on page size selection', () => {
    component.pageSize = 10;

    const sizeSpy = vi.fn();
    component.pageSizeChange.subscribe(sizeSpy);

    component.onPageSizeChange(25);
    expect(sizeSpy).toHaveBeenCalledWith(25);
  });
});
