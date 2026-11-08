import { DatePipe, DecimalPipe } from '@angular/common';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  Injector,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { apiErrorMessage } from '../../shared/api-error';
import { BrowserPrint } from '../../shared/browser-print';
import { IconComponent } from '../../shared/icon';
import { InvoiceStatusComponent } from '../../shared/invoice-status';
import { PageHeaderComponent } from '../../shared/page-header';
import { StateComponent } from '../../shared/state';
import { InvoiceApi } from './invoice-api';
import { Invoice } from './invoice.models';

@Component({
  selector: 'app-invoice-detail',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    IconComponent,
    InvoiceStatusComponent,
    PageHeaderComponent,
    StateComponent,
  ],
  templateUrl: './invoice-detail.html',
  styleUrl: './invoice-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetail implements OnInit {
  private readonly api = inject(InvoiceApi);
  private readonly route = inject(ActivatedRoute);
  private readonly browserPrint = inject(BrowserPrint);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly invoiceId = this.route.snapshot.paramMap.get('id');

  readonly invoice = signal<Invoice | null>(null);
  readonly loading = signal(false);
  readonly printing = signal(false);
  readonly recovering = signal(false);
  readonly stateKnown = signal(false);
  readonly errorMessage = signal('');
  readonly notice = signal('');
  readonly pending = computed(() => this.printing() || this.recovering());
  readonly canPrint = computed(
    () =>
      this.invoice()?.status === 'Open' && this.stateKnown() && !this.loading() && !this.pending(),
  );
  readonly totalQuantity = computed(
    () => this.invoice()?.items.reduce((total, item) => total + item.quantity, 0) ?? 0,
  );

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    if (this.loading() || this.pending()) return;

    if (!this.invoiceId) {
      this.errorMessage.set('Nota não encontrada. Volte à lista e selecione uma nota.');
      return;
    }

    const confirmingState = this.invoice() !== null && !this.stateKnown();
    this.loading.set(true);
    this.stateKnown.set(false);
    this.errorMessage.set('');
    this.notice.set('');

    this.api
      .get(this.invoiceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (invoice) => {
          this.invoice.set(invoice);
          this.stateKnown.set(true);
          this.loading.set(false);
          if (confirmingState) this.notice.set(this.confirmedStateMessage(invoice));
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.errorMessage.set(apiErrorMessage(error, 'Não foi possível consultar a nota.'));
        },
      });
  }

  print(): void {
    const invoice = this.invoice();
    if (!invoice || !this.canPrint()) return;

    this.printing.set(true);
    this.stateKnown.set(false);
    this.errorMessage.set('');
    this.notice.set('');

    this.api
      .print(invoice.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (confirmedInvoice) => {
          if (confirmedInvoice.status !== 'Closed') {
            this.recoverAfterPrintError(
              new Error('A resposta não confirmou o fechamento da nota.'),
            );
            return;
          }

          this.invoice.set(confirmedInvoice);
          this.printing.set(false);
          this.stateKnown.set(true);
          this.notice.set('Nota fechada e estoque atualizado. O diálogo de impressão será aberto.');

          // Aguarda o Angular renderizar os dados confirmados antes de imprimir a página.
          afterNextRender(
            () => {
              try {
                this.browserPrint.print();
              } catch {
                this.notice.set(
                  'Nota fechada e estoque atualizado. O navegador não conseguiu abrir o diálogo de impressão.',
                );
              }
            },
            { injector: this.injector },
          );
        },
        error: (error: unknown) => this.recoverAfterPrintError(error),
      });
  }

  private recoverAfterPrintError(error: unknown): void {
    const invoice = this.invoice();
    if (!invoice) return;

    this.printing.set(false);
    this.recovering.set(true);
    this.stateKnown.set(false);
    this.errorMessage.set(apiErrorMessage(error, 'Não foi possível confirmar a impressão.'));

    // Uma resposta perdida pode ter ocorrido depois do fechamento. Consultar não repete a baixa.
    this.api
      .get(invoice.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (confirmedInvoice) => {
          this.invoice.set(confirmedInvoice);
          this.stateKnown.set(true);
          this.recovering.set(false);
          this.notice.set(this.confirmedStateMessage(confirmedInvoice));
        },
        error: () => {
          this.recovering.set(false);
          this.notice.set(
            'O estado da nota não foi confirmado. Consulte novamente antes de imprimir.',
          );
        },
      });
  }

  private confirmedStateMessage(invoice: Invoice): string {
    return invoice.status === 'Closed'
      ? 'Consulta confirmada: a nota está fechada. A impressão não será solicitada novamente.'
      : 'Consulta confirmada: a nota continua aberta. Após resolver a causa, tente imprimir esta mesma nota.';
  }
}
