import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { InvoiceApi } from '../features/invoices/invoice-api';
import { ProductApi } from '../features/products/product-api';
import { apiErrorMessage, apiFieldErrors } from './api-error';

describe('Contratos HTTP das APIs', () => {
  let http: HttpTestingController;
  let products: ProductApi;
  let invoices: InvoiceApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    products = TestBed.inject(ProductApi);
    invoices = TestBed.inject(InvoiceApi);
  });

  afterEach(() => http.verify());

  it('consulta produtos com busca normalizada e paginação iniciada em um', () => {
    products.list('  Parafuso  ', 2, 20).subscribe();
    const request = http.expectOne((req) => req.url === `${environment.stockApiUrl}/products`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('Parafuso');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('envia edição por PUT com versão e metadados, sem alteração de saldo', () => {
    const payload = {
      code: 'P-01',
      description: 'Produto',
      rowVersion: 'AAAAAAAAAAE=',
      minimumStock: 3,
      category: 'Peças',
    };
    products.update('produto-1', payload).subscribe();
    const request = http.expectOne(`${environment.stockApiUrl}/products/produto-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(payload);
    expect(request.request.body).not.toHaveProperty('balance');
    request.flush({ id: 'produto-1', ...payload, balance: 10 });
  });

  it('envia somente a descrição ao Stock e recebe a sugestão sem salvar o produto', () => {
    const received = vi.fn();
    products.improveDescription('Parafuso aço').subscribe(received);

    const request = http.expectOne(`${environment.stockApiUrl}/products/improve-description`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ description: 'Parafuso aço' });
    request.flush({ description: 'Parafuso de aço' });
    expect(received).toHaveBeenCalledWith({ description: 'Parafuso de aço' });
    http.expectNone(`${environment.stockApiUrl}/products`);
  });

  it('envia filtros de número e status para o serviço de notas', () => {
    invoices.list(3, 10, 42, 'Open').subscribe();
    const request = http.expectOne((req) => req.url === `${environment.billingApiUrl}/invoices`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('number')).toBe('42');
    expect(request.request.params.get('status')).toBe('Open');
    request.flush({ items: [], page: 3, pageSize: 10, totalCount: 0 });
  });

  it('solicita impressão com POST sem corpo e entrega a nota fechada recebida', () => {
    const received = vi.fn();
    const closed = {
      id: 'nota-1',
      number: 42,
      status: 'Closed',
      createdAt: '2026-09-09T12:00:00Z',
      closedAt: '2026-09-09T12:01:00Z',
      items: [],
    };
    invoices.print('nota-1').subscribe(received);
    const request = http.expectOne(`${environment.billingApiUrl}/invoices/nota-1/print`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.headers.has('Idempotency-Key')).toBe(false);
    request.flush(closed);
    expect(received).toHaveBeenCalledWith(closed);
  });

  it('entrega a falha de impressão sem repetir automaticamente o POST', () => {
    const failed = vi.fn();
    invoices.print('nota-1').subscribe({ error: failed });
    const request = http.expectOne(`${environment.billingApiUrl}/invoices/nota-1/print`);
    request.flush(
      { title: 'STOCK_UNAVAILABLE', detail: 'O estoque está indisponível.' },
      { status: 503, statusText: 'Service Unavailable' },
    );
    expect(failed).toHaveBeenCalledOnce();
    expect(failed.mock.calls[0]?.[0]).toBeInstanceOf(HttpErrorResponse);
    http.expectNone(`${environment.billingApiUrl}/invoices/nota-1/print`);
  });
});

describe('Mensagens dos dois serviços', () => {
  it('prioriza a mensagem detail do Billing sobre seu código técnico', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: { title: 'INSUFFICIENT_STOCK', detail: 'Saldo insuficiente para o produto P-01.' },
    });
    expect(apiErrorMessage(error)).toBe('Saldo insuficiente para o produto P-01.');
  });

  it('usa o título legível do Stock e uma mensagem alternativa para códigos técnicos', () => {
    expect(
      apiErrorMessage(
        new HttpErrorResponse({
          status: 409,
          error: { title: 'Já existe um produto com este código.' },
        }),
      ),
    ).toBe('Já existe um produto com este código.');
    expect(
      apiErrorMessage(
        new HttpErrorResponse({ status: 500, error: { title: 'INTERNAL_ERROR' } }),
        'Não foi possível salvar.',
      ),
    ).toBe('Não foi possível salvar.');
    expect(apiErrorMessage(new Error('Detalhe interno'), 'Não foi possível salvar.')).toBe(
      'Não foi possível salvar.',
    );
  });

  it('normaliza nomes de campos e ignora mensagens de validação inválidas', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        errors: {
          Code: ['Informe o código.', 123],
          '$.Balance': ['Informe um saldo inteiro.'],
          Description: 'Formato inesperado',
        },
      },
    });
    expect(apiFieldErrors(error)).toEqual({
      code: ['Informe o código.'],
      balance: ['Informe um saldo inteiro.'],
    });
    expect(apiErrorMessage(error)).toBe('Informe o código. Informe um saldo inteiro.');
  });

  it('mostra uma mensagem de conexão quando não há resposta HTTP', () => {
    const error = new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') });
    expect(apiErrorMessage(error)).toBe(
      'Não foi possível conectar ao serviço. Verifique a conexão e tente novamente.',
    );
    expect(apiFieldErrors(error)).toEqual({});
  });
});
