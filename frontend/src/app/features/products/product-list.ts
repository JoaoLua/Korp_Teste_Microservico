import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  BehaviorSubject,
  catchError,
  debounceTime,
  defer,
  distinctUntilChanged,
  finalize,
  of,
  switchMap,
} from 'rxjs';
import { apiErrorMessage } from '../../shared/api-error';
import { IconComponent } from '../../shared/icon';
import { PageHeaderComponent } from '../../shared/page-header';
import { PaginationComponent } from '../../shared/pagination';
import { StateComponent } from '../../shared/state';
import { ProductApi } from './product-api';
import { Product } from './product.models';

@Component({
  selector: 'app-product-list',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    IconComponent,
    PageHeaderComponent,
    PaginationComponent,
    StateComponent,
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductList {
  private readonly api = inject(ProductApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly queries = new BehaviorSubject({ search: '', page: 1, pageSize: 10 });

  readonly search = new FormControl('', { nonNullable: true });
  readonly products = signal<Product[]>([]);
  readonly total = signal<number | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeSearch = signal('');

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((search) => {
        this.activeSearch.set(search.trim());
        this.page.set(1);
        this.load();
      });

    this.queries
      .pipe(
        switchMap((query) =>
          defer(() => {
            this.loading.set(true);
            this.error.set(null);
            return this.api.list(query.search, query.page, query.pageSize).pipe(
              catchError((error) => {
                this.error.set(apiErrorMessage(error, 'Não foi possível carregar os produtos.'));
                this.total.set(null);
                return of(null);
              }),
              finalize(() => this.loading.set(false)),
            );
          }),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        if (!result) return;
        this.products.set(result.items);
        this.total.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
      });
  }

  load(): void {
    this.queries.next({
      search: this.activeSearch(),
      page: this.page(),
      pageSize: this.pageSize(),
    });
  }

  changePage(event: { page: number; pageSize: number }): void {
    this.page.set(event.page);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  clearSearch(): void {
    this.search.setValue('');
  }
}
