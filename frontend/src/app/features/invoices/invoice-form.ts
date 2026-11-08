import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { InvoiceApi } from './invoice-api';
import { ProductApi } from '../products/product-api';
import { Product } from '../products/product.models';
import { PageHeaderComponent } from '../../shared/page-header';
import { StateComponent } from '../../shared/state';
import { PaginationComponent } from '../../shared/pagination';
import { IconComponent } from '../../shared/icon';
import { apiErrorMessage } from '../../shared/api-error';
import { integerRange, INT_MAX } from '../../shared/validators';

type InvoiceItemForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number | null>;
}>;

@Component({
  selector: 'app-invoice-form',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    PageHeaderComponent,
    StateComponent,
    PaginationComponent,
    IconComponent,
  ],
  templateUrl: './invoice-form.html',
  styleUrl: './invoice-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceForm implements OnInit {
  private readonly api = inject(InvoiceApi);
  private readonly productsApi = inject(ProductApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly productRequests = new Subject<{
    search: string;
    page: number;
    pageSize: number;
  }>();

  readonly maxQuantity = INT_MAX;
  readonly items = new FormArray<InvoiceItemForm>(
    [],
    [Validators.required, Validators.maxLength(100)],
  );
  readonly form = new FormGroup({ items: this.items });
  readonly search = new FormControl('', { nonNullable: true });
  readonly products = signal<Product[]>([]);
  readonly selectedProducts = signal<Record<string, Product>>({});
  readonly productsTotal = signal(0);
  readonly productsPage = signal(1);
  readonly productsPageSize = signal(5);
  readonly productsLoading = signal(true);
  readonly productsError = signal('');
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly error = signal('');
  readonly creationUncertain = signal(false);

  constructor() {
    this.productRequests
      .pipe(
        tap(() => {
          this.productsLoading.set(true);
          this.productsError.set('');
        }),
        switchMap((query) =>
          this.productsApi.list(query.search, query.page, query.pageSize).pipe(
            catchError((error) => {
              this.productsError.set(
                apiErrorMessage(error, 'Não foi possível consultar os produtos.'),
              );
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        if (result) {
          this.products.set(result.items);
          this.productsTotal.set(result.totalCount);
          this.productsPage.set(result.page);
          this.productsPageSize.set(result.pageSize);
        }
        this.productsLoading.set(false);
      });

    this.search.valueChanges
      .pipe(
        map((value) => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.productsPage.set(1);
        this.loadProducts();
      });
  }

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.productRequests.next({
      search: this.search.value.trim(),
      page: this.productsPage(),
      pageSize: this.productsPageSize(),
    });
  }

  changeProductPage(event: { page: number; pageSize: number }): void {
    this.productsPage.set(event.page);
    this.productsPageSize.set(event.pageSize);
    this.loadProducts();
  }

  isSelected(id: string): boolean {
    return this.items.controls.some((item) => item.controls.productId.value === id);
  }

  addProduct(product: Product): void {
    if (
      this.saving() ||
      this.creationUncertain() ||
      this.items.length >= 100 ||
      this.isSelected(product.id)
    )
      return;
    this.selectedProducts.update((selected) => ({ ...selected, [product.id]: product }));
    this.items.push(
      new FormGroup({
        productId: new FormControl(product.id, {
          nonNullable: true,
          validators: Validators.required,
        }),
        quantity: new FormControl<number | null>(1, [
          Validators.required,
          integerRange(1, INT_MAX),
        ]),
      }),
    );
    this.error.set('');
  }

  removeProduct(index: number): void {
    if (this.saving() || this.creationUncertain()) return;
    const id = this.items.at(index)?.controls.productId.value;
    if (!id) return;
    this.items.removeAt(index);
    this.selectedProducts.update((selected) => {
      const updated = { ...selected };
      delete updated[id];
      return updated;
    });
  }

  submit(): void {
    if (this.saving() || this.creationUncertain()) return;
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const items = this.items
      .getRawValue()
      .map((item) => ({ productId: item.productId, quantity: item.quantity! }));
    if (new Set(items.map((item) => item.productId)).size !== items.length) {
      this.error.set(
        'Cada produto deve aparecer uma única vez na nota. Ajuste a quantidade do item existente.',
      );
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.api
      .create({ items })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (invoice) => {
          void this.router.navigate(['/notas', invoice.id]);
        },
        error: (error) => {
          this.saving.set(false);
          this.error.set(apiErrorMessage(error, 'Não foi possível criar a nota fiscal.'));
          if (error?.status === 0 || error?.status >= 500) {
            this.creationUncertain.set(true);
          }
        },
      });
  }
}
