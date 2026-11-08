import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { InvoiceApi } from './invoice-api';
import { InvoiceForm } from './invoice-form';
import { Invoice } from './invoice.models';
import { ProductApi } from '../products/product-api';
import { Product } from '../products/product.models';

function product(id: string, balance = 10): Product {
  return {
    id,
    code: `PROD-${id}`,
    description: `Produto ${id}`,
    balance,
    minimumStock: 0,
    category: null,
    createdAt: '2026-09-09T12:00:00Z',
    updatedAt: null,
    rowVersion: 'AAAAAAAAAAE=',
  };
}

describe('InvoiceForm', () => {
  const firstProduct = product('1');
  let component: InvoiceForm;
  let create: ReturnType<typeof vi.fn>;
  let listProducts: ReturnType<typeof vi.fn>;
  let response: Subject<Invoice>;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    response = new Subject<Invoice>();
    create = vi.fn().mockReturnValue(response);
    listProducts = vi
      .fn()
      .mockReturnValue(of({ items: [firstProduct], page: 1, pageSize: 5, totalCount: 2 }));
    TestBed.configureTestingModule({
      imports: [InvoiceForm],
      providers: [
        provideRouter([]),
        { provide: InvoiceApi, useValue: { create } },
        { provide: ProductApi, useValue: { list: listProducts } },
      ],
    });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    component = TestBed.createComponent(InvoiceForm).componentInstance;
  });

  it('mantém os itens selecionados ao consultar outra página de produtos', () => {
    component.ngOnInit();
    component.addProduct(firstProduct);
    listProducts.mockReturnValue(
      of({ items: [product('2')], page: 2, pageSize: 5, totalCount: 2 }),
    );

    component.changeProductPage({ page: 2, pageSize: 5 });

    expect(listProducts).toHaveBeenLastCalledWith('', 2, 5);
    expect(component.products()[0].id).toBe('2');
    expect(component.items.getRawValue()).toEqual([{ productId: '1', quantity: 1 }]);
    expect(component.selectedProducts()['1']).toEqual(firstProduct);
  });

  it('mantém cada produto uma única vez e permite adicioná-lo novamente depois da remoção', () => {
    component.addProduct(firstProduct);
    component.addProduct(firstProduct);
    expect(component.items.length).toBe(1);

    component.removeProduct(0);
    expect(component.isSelected('1')).toBe(false);
    component.addProduct(firstProduct);
    expect(component.items.length).toBe(1);
  });

  it('limita a nota a 100 produtos distintos', () => {
    for (let index = 0; index < 101; index++) component.addProduct(product(String(index)));
    expect(component.items.length).toBe(100);
  });

  it('exige um item e impede quantidades vazias, fracionadas, negativas ou fora do limite', () => {
    component.submit();
    expect(create).not.toHaveBeenCalled();
    component.addProduct(firstProduct);

    for (const value of [null, 0, -1, 1.5, 2147483648]) {
      component.items.at(0).controls.quantity.setValue(value);
      component.submit();
    }

    expect(create).not.toHaveBeenCalled();
    expect(component.saving()).toBe(false);
  });

  it('envia somente IDs e quantidades, sem limitar a criação ao saldo exibido', () => {
    component.addProduct(product('1', 0));
    component.items.at(0).controls.quantity.setValue(2);
    component.submit();

    expect(create).toHaveBeenCalledExactlyOnceWith({ items: [{ productId: '1', quantity: 2 }] });
    response.next({
      id: 'note-1',
      number: 1,
      status: 'Open',
      createdAt: '2026-09-09T12:00:00Z',
      closedAt: null,
      items: [],
    });
    expect(navigate).toHaveBeenCalledWith(['/notas', 'note-1']);
  });

  it('impede um segundo envio e mudanças nos itens enquanto a criação está pendente', () => {
    component.addProduct(firstProduct);
    component.submit();
    component.submit();
    component.removeProduct(0);
    component.addProduct(product('2'));

    expect(create).toHaveBeenCalledTimes(1);
    expect(component.items.length).toBe(1);
    expect(component.saving()).toBe(true);
  });

  it('preserva o rascunho e impede nova criação quando a resposta pode ter sido perdida', () => {
    component.addProduct(firstProduct);
    component.submit();
    response.error(new HttpErrorResponse({ status: 0, statusText: 'Unknown Error' }));
    component.submit();

    expect(component.creationUncertain()).toBe(true);
    expect(component.saving()).toBe(false);
    expect(component.items.length).toBe(1);
    expect(create).toHaveBeenCalledTimes(1);
    expect(navigate).not.toHaveBeenCalled();
  });
});
