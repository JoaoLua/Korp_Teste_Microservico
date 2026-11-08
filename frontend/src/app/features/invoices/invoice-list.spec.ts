import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { InvoiceApi } from './invoice-api';
import { InvoiceList } from './invoice-list';

describe('InvoiceList', () => {
  let component: InvoiceList;
  let list: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    list = vi.fn().mockReturnValue(of({ items: [], page: 1, pageSize: 10, totalCount: 0 }));
    TestBed.configureTestingModule({
      imports: [InvoiceList],
      providers: [provideRouter([]), { provide: InvoiceApi, useValue: { list } }],
    });
    component = TestBed.createComponent(InvoiceList).componentInstance;
  });

  it('envia o número exato e o status e mantém os filtros na paginação', () => {
    component.filters.setValue({ number: 12, status: 'Open' });
    component.applyFilters();
    expect(list).toHaveBeenLastCalledWith(1, 10, 12, 'Open');

    component.changePage({ page: 2, pageSize: 20 });
    expect(list).toHaveBeenLastCalledWith(2, 20, 12, 'Open');
  });

  it('aceita a consulta sem número e rejeita valores fracionados ou inseguros', () => {
    component.applyFilters();
    expect(list).toHaveBeenCalledExactlyOnceWith(1, 10, undefined, undefined);

    for (const number of [0, -1, 1.5, Number.MAX_SAFE_INTEGER + 1]) {
      component.filters.controls.number.setValue(number);
      component.applyFilters();
    }

    expect(list).toHaveBeenCalledTimes(1);
  });

  it('limpa os filtros e volta para a primeira página', () => {
    component.filters.setValue({ number: 7, status: 'Closed' });
    component.applyFilters();
    component.changePage({ page: 3, pageSize: 10 });
    component.clearFilters();

    expect(list).toHaveBeenLastCalledWith(1, 10, undefined, undefined);
    expect(component.filtered()).toBe(false);
    expect(component.filters.getRawValue()).toEqual({ number: null, status: '' });
  });

  it('ignora uma consulta antiga que terminou depois da troca de filtro', () => {
    const oldRequest = new Subject<{
      items: never[];
      page: number;
      pageSize: number;
      totalCount: number;
    }>();
    const currentRequest = new Subject<{
      items: never[];
      page: number;
      pageSize: number;
      totalCount: number;
    }>();
    list.mockReturnValueOnce(oldRequest).mockReturnValueOnce(currentRequest);
    component.ngOnInit();
    component.filters.controls.status.setValue('Closed');
    component.applyFilters();
    currentRequest.next({ items: [], page: 1, pageSize: 10, totalCount: 2 });
    oldRequest.next({ items: [], page: 1, pageSize: 10, totalCount: 99 });

    expect(component.total()).toBe(2);
    expect(component.loading()).toBe(false);
  });
});
