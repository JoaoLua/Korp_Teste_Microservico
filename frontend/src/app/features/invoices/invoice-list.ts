import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of, Subject, switchMap, tap } from 'rxjs';
import { InvoiceApi } from './invoice-api';
import { Invoice, InvoiceStatus } from './invoice.models';
import { PageHeaderComponent } from '../../shared/page-header';
import { StateComponent } from '../../shared/state';
import { PaginationComponent } from '../../shared/pagination';
import { InvoiceStatusComponent } from '../../shared/invoice-status';
import { IconComponent } from '../../shared/icon';
import { apiErrorMessage } from '../../shared/api-error';
import { integerRange } from '../../shared/validators';

interface InvoiceQuery {
  page: number;
  pageSize: number;
  number?: number;
  status?: InvoiceStatus;
}

@Component({
  selector: 'app-invoice-list',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    PageHeaderComponent,
    StateComponent,
    PaginationComponent,
    InvoiceStatusComponent,
    IconComponent,
  ],
  templateUrl: './invoice-list.html',
  styleUrl: './invoice-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceList implements OnInit {
  private readonly api = inject(InvoiceApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<InvoiceQuery>();
  private appliedFilters: Pick<InvoiceQuery, 'number' | 'status'> = {};

  readonly filters = new FormGroup({
    number: new FormControl<number | null>(null, (control) =>
      control.value === null || control.value === ''
        ? null
        : integerRange(1, Number.MAX_SAFE_INTEGER)(control),
    ),
    status: new FormControl<InvoiceStatus | ''>('', { nonNullable: true }),
  });
  readonly invoices = signal<Invoice[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly filtered = signal(false);

  constructor() {
    this.requests
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set('');
        }),
        switchMap((query) =>
          this.api.list(query.page, query.pageSize, query.number, query.status).pipe(
            catchError((error) => {
              this.error.set(apiErrorMessage(error, 'Não foi possível carregar as notas fiscais.'));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        if (result) {
          this.invoices.set(result.items);
          this.total.set(result.totalCount);
          this.page.set(result.page);
          this.pageSize.set(result.pageSize);
        }
        this.loading.set(false);
      });
  }

  ngOnInit(): void {
    this.reload();
  }

  applyFilters(): void {
    this.filters.markAllAsTouched();
    if (this.filters.invalid) return;
    const values = this.filters.getRawValue();
    this.appliedFilters = {
      number: values.number ?? undefined,
      status: values.status || undefined,
    };
    this.filtered.set(values.number !== null || values.status !== '');
    this.page.set(1);
    this.reload();
  }

  clearFilters(): void {
    this.filters.reset({ number: null, status: '' });
    this.applyFilters();
  }

  changePage(event: { page: number; pageSize: number }): void {
    this.page.set(event.page);
    this.pageSize.set(event.pageSize);
    this.reload();
  }

  reload(): void {
    this.requests.next({ page: this.page(), pageSize: this.pageSize(), ...this.appliedFilters });
  }
}
