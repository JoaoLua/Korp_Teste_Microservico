import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { BrowserPrint } from '../../shared/browser-print';
import { InvoiceApi } from './invoice-api';
import { InvoiceDetail } from './invoice-detail';
import { Invoice } from './invoice.models';

const openInvoice: Invoice = {
  id: '81d63b04-dd45-4b2a-95c5-6a0591934584',
  number: 24,
  status: 'Open',
  createdAt: '2026-09-09T12:00:00Z',
  closedAt: null,
  items: [
    {
      id: '307b0200-31db-466a-94ad-03004668cb8c',
      productId: 'da96b99b-25d4-4a5b-9bf6-964b419f4916',
      productCode: 'TESTE-01',
      productDescription: 'Produto usado no teste unitário',
      quantity: 2,
    },
  ],
};
const closedInvoice: Invoice = {
  ...openInvoice,
  status: 'Closed',
  closedAt: '2026-09-09T12:05:00Z',
};
const unavailable = new HttpErrorResponse({
  status: 503,
  error: { detail: 'O serviço de estoque está indisponível.' },
});

describe('InvoiceDetail', () => {
  let fixture: ComponentFixture<InvoiceDetail>;
  let component: InvoiceDetail;
  let api: { get: ReturnType<typeof vi.fn>; print: ReturnType<typeof vi.fn> };
  let browserPrint: { print: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = { get: vi.fn().mockReturnValue(of(openInvoice)), print: vi.fn() };
    browserPrint = { print: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [InvoiceDetail],
      providers: [
        provideRouter([]),
        { provide: InvoiceApi, useValue: api },
        { provide: BrowserPrint, useValue: browserPrint },
      ],
    }).compileComponents();

    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot.paramMap, 'get').mockImplementation((name) =>
      name === 'id' ? openInvoice.id : null,
    );
  });

  afterEach(() => vi.restoreAllMocks());

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(InvoiceDetail);
    component = fixture.componentInstance;
    await fixture.whenStable();
  }

  it('carrega a nota sem imprimir e mostra as quantidades retornadas', async () => {
    await render();

    expect(api.get).toHaveBeenCalledExactlyOnceWith(openInvoice.id);
    expect(api.print).not.toHaveBeenCalled();
    expect(browserPrint.print).not.toHaveBeenCalled();
    expect(component.canPrint()).toBe(true);
    expect(component.totalQuantity()).toBe(2);
    expect(fixture.nativeElement.textContent).toContain(openInvoice.items[0].productDescription);
  });

  it('mantém a impressão bloqueada enquanto carrega', async () => {
    const loading = new Subject<Invoice>();
    api.get.mockReturnValue(loading);
    await render();

    expect(component.loading()).toBe(true);
    expect(component.canPrint()).toBe(false);
    component.print();
    expect(api.print).not.toHaveBeenCalled();

    loading.next(openInvoice);
    loading.complete();
    await fixture.whenStable();
    expect(component.canPrint()).toBe(true);
  });

  it('envia um único POST mesmo com dois cliques enquanto processa', async () => {
    const printing = new Subject<Invoice>();
    api.print.mockReturnValue(printing);
    await render();

    component.print();
    component.print();
    await fixture.whenStable();

    expect(api.print).toHaveBeenCalledExactlyOnceWith(openInvoice.id);
    expect(component.printing()).toBe(true);
    expect(component.canPrint()).toBe(false);
    const button = fixture.nativeElement.querySelector('.button--primary') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Imprimindo');
  });

  it('renderiza a nota fechada antes de abrir o diálogo do navegador', async () => {
    const printing = new Subject<Invoice>();
    api.print.mockReturnValue(printing);
    await render();
    let documentAtPrint = '';
    let printButtonAtPrint: Element | null = null;
    browserPrint.print.mockImplementation(() => {
      documentAtPrint = fixture.nativeElement.querySelector('.print-document').textContent;
      printButtonAtPrint = fixture.nativeElement.querySelector('.button--primary');
    });

    component.print();
    printing.next(closedInvoice);
    printing.complete();
    await fixture.whenStable();

    expect(component.invoice()).toEqual(closedInvoice);
    expect(component.canPrint()).toBe(false);
    expect(browserPrint.print).toHaveBeenCalledTimes(1);
    expect(documentAtPrint).toContain('Fechada');
    expect(printButtonAtPrint).toBeNull();
    component.print();
    expect(api.print).toHaveBeenCalledTimes(1);
  });

  it('não oferece impressão nem abre o diálogo ao carregar uma nota fechada', async () => {
    api.get.mockReturnValue(of(closedInvoice));
    await render();

    component.print();

    expect(component.canPrint()).toBe(false);
    expect(api.print).not.toHaveBeenCalled();
    expect(browserPrint.print).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.button--primary')).toBeNull();
  });

  it('após falha consulta uma vez e bloqueia nova impressão se a nota já fechou', async () => {
    api.get.mockReturnValueOnce(of(openInvoice)).mockReturnValueOnce(of(closedInvoice));
    api.print.mockReturnValue(throwError(() => unavailable));
    await render();

    component.print();
    await fixture.whenStable();

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.print).toHaveBeenCalledTimes(1);
    expect(component.invoice()?.status).toBe('Closed');
    expect(component.canPrint()).toBe(false);
    expect(browserPrint.print).not.toHaveBeenCalled();
    expect(component.notice()).toContain('a nota está fechada');
  });

  it('permite repetição manual na mesma nota aberta, sem repetir automaticamente o POST', async () => {
    api.print.mockReturnValueOnce(throwError(() => unavailable));
    await render();

    component.print();
    await fixture.whenStable();

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.print).toHaveBeenCalledTimes(1);
    expect(component.canPrint()).toBe(true);
    expect(component.errorMessage()).toContain('estoque está indisponível');
    expect(browserPrint.print).not.toHaveBeenCalled();

    const retry = new Subject<Invoice>();
    api.print.mockReturnValueOnce(retry);
    component.print();
    expect(api.print).toHaveBeenLastCalledWith(openInvoice.id);
    expect(api.print).toHaveBeenCalledTimes(2);
  });

  it('bloqueia impressão enquanto consulta a situação após uma falha', async () => {
    const recovery = new Subject<Invoice>();
    api.get.mockReturnValueOnce(of(openInvoice)).mockReturnValueOnce(recovery);
    api.print.mockReturnValue(throwError(() => unavailable));
    await render();

    component.print();
    component.print();
    component.refresh();

    expect(component.recovering()).toBe(true);
    expect(component.canPrint()).toBe(false);
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.print).toHaveBeenCalledTimes(1);

    recovery.next(openInvoice);
    recovery.complete();
    expect(component.canPrint()).toBe(true);
  });

  it('exige consulta manual quando o estado é desconhecido e reabilita somente após confirmar', async () => {
    api.get.mockReturnValueOnce(of(openInvoice)).mockReturnValueOnce(throwError(() => unavailable));
    api.print.mockReturnValue(throwError(() => unavailable));
    await render();

    component.print();
    await fixture.whenStable();
    component.print();

    expect(component.stateKnown()).toBe(false);
    expect(component.canPrint()).toBe(false);
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(api.print).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Consultar novamente');

    api.get.mockReturnValueOnce(of(openInvoice));
    component.refresh();
    await fixture.whenStable();

    expect(component.stateKnown()).toBe(true);
    expect(component.canPrint()).toBe(true);
    expect(api.get).toHaveBeenCalledTimes(3);
    expect(api.print).toHaveBeenCalledTimes(1);
    expect(browserPrint.print).not.toHaveBeenCalled();
  });

  it('mostra erro inicial com uma ação de nova consulta', async () => {
    api.get.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 404,
            error: { detail: 'A nota solicitada não foi encontrada.' },
          }),
      ),
    );
    await render();

    expect(component.invoice()).toBeNull();
    expect(component.canPrint()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('A nota solicitada não foi encontrada.');
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
  });
});
