import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage, apiFieldErrors } from '../../shared/api-error';
import { IconComponent } from '../../shared/icon';
import { PageHeaderComponent } from '../../shared/page-header';
import { StateComponent } from '../../shared/state';
import { INT_MAX, integerRange, trimmedRequired } from '../../shared/validators';
import { ProductApi } from './product-api';
import { Product } from './product.models';

@Component({
  selector: 'app-product-form',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    IconComponent,
    PageHeaderComponent,
    StateComponent,
  ],
  templateUrl: './product-form.html',
  styleUrl: './product-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductForm implements OnInit {
  private readonly api = inject(ProductApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  readonly id = this.route.snapshot.paramMap.get('id');
  readonly integerMax = INT_MAX;
  readonly product = signal<Product | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly adjusting = signal(false);
  readonly adjustmentOpen = signal(false);
  readonly needsReload = signal(false);
  readonly creationUncertain = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string[]>>({});
  readonly adjustmentErrors = signal<Record<string, string[]>>({});
  readonly improvingDescription = signal(false);
  readonly descriptionSuggestion = signal<string | null>(null);
  readonly descriptionImprovementError = signal<string | null>(null);
  private descriptionRevision = 0;
  private suggestionSource: string | null = null;

  readonly form = this.fb.nonNullable.group({
    code: ['', [trimmedRequired, Validators.maxLength(50)]],
    description: ['', [trimmedRequired, Validators.maxLength(200)]],
    balance: [0, [Validators.required, integerRange(0, INT_MAX)]],
  });
  readonly adjustmentForm = this.fb.nonNullable.group({
    balance: [0, [Validators.required, integerRange(0, INT_MAX)]],
    reason: ['', [trimmedRequired, Validators.maxLength(200)]],
  });

  constructor() {
    this.form.controls.description.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.descriptionRevision++;
        this.discardDescriptionSuggestion();
        this.descriptionImprovementError.set(null);
      });
  }

  ngOnInit(): void {
    if (this.id) this.reload();
  }

  improveDescription(): void {
    if (
      this.improvingDescription() ||
      this.saving() ||
      this.adjusting() ||
      this.loading() ||
      this.needsReload() ||
      this.creationUncertain()
    )
      return;
    const control = this.form.controls.description;
    control.markAsTouched();
    if (control.invalid) return;
    const source = control.value;
    const revision = this.descriptionRevision;
    this.discardDescriptionSuggestion();
    this.descriptionImprovementError.set(null);
    this.improvingDescription.set(true);
    this.api
      .improveDescription(source.trim())
      .pipe(
        finalize(() => this.improvingDescription.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => {
          if (revision !== this.descriptionRevision) return;
          const suggestion =
            typeof response?.description === 'string' ? response.description.trim() : '';
          if (!suggestion || suggestion.length > 200) {
            this.descriptionImprovementError.set(
              'Não foi possível gerar uma descrição válida. Você pode editar o texto e salvar normalmente.',
            );
            return;
          }
          this.suggestionSource = source;
          this.descriptionSuggestion.set(suggestion);
        },
        error: (error) => {
          if (revision !== this.descriptionRevision) return;
          this.descriptionImprovementError.set(
            apiErrorMessage(error, 'Não foi possível melhorar a descrição. Tente novamente.'),
          );
        },
      });
  }

  useDescriptionSuggestion(): void {
    const suggestion = this.descriptionSuggestion();
    if (
      !suggestion ||
      this.saving() ||
      this.adjusting() ||
      this.loading() ||
      this.needsReload() ||
      this.creationUncertain()
    )
      return;
    if (this.form.controls.description.value !== this.suggestionSource) {
      this.discardDescriptionSuggestion();
      return;
    }
    this.form.controls.description.setValue(suggestion);
    this.form.controls.description.markAsDirty();
    this.form.controls.description.markAsTouched();
  }

  discardDescriptionSuggestion(): void {
    this.descriptionSuggestion.set(null);
    this.suggestionSource = null;
  }

  reload(): void {
    if (!this.id || this.loading() || this.saving() || this.adjusting()) return;
    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .get(this.id)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (product) => {
          this.product.set(product);
          this.form.reset({
            code: product.code,
            description: product.description,
            balance: product.balance,
          });
          this.adjustmentForm.reset({ balance: product.balance, reason: '' });
          this.needsReload.set(false);
          this.error.set(null);
          this.success.set(null);
          this.serverErrors.set({});
          this.adjustmentErrors.set({});
        },
        error: (error) =>
          this.loadError.set(apiErrorMessage(error, 'Não foi possível carregar o produto.')),
      });
  }

  save(): void {
    if (
      this.saving() ||
      this.adjusting() ||
      this.needsReload() ||
      this.creationUncertain() ||
      this.loading()
    )
      return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const values = this.form.getRawValue();
    const product = this.product();
    if (this.id && !product) return;
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.serverErrors.set({});
    const request =
      this.id && product
        ? this.api.update(this.id, {
            code: values.code.trim(),
            description: values.description.trim(),
            rowVersion: product.rowVersion,
            minimumStock: product.minimumStock,
            category: product.category,
          })
        : this.api.create({
            code: values.code.trim(),
            description: values.description.trim(),
            balance: values.balance,
          });
    request
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (updated) => {
          if (!this.id) {
            void this.router.navigate(['/produtos']);
            return;
          }
          this.product.set(updated);
          this.form.reset({
            code: updated.code,
            description: updated.description,
            balance: updated.balance,
          });
          this.adjustmentForm.controls.balance.setValue(updated.balance);
          this.success.set('Produto atualizado.');
        },
        error: (error) => {
          this.serverErrors.set(apiFieldErrors(error));
          this.handleError(error, 'Não foi possível salvar o produto.');
        },
      });
  }

  toggleAdjustment(): void {
    this.adjustmentOpen.update((open) => !open);
    if (this.adjustmentOpen()) {
      this.adjustmentForm.reset({ balance: this.product()?.balance ?? 0, reason: '' });
      this.adjustmentErrors.set({});
    }
  }

  adjust(): void {
    const product = this.product();
    if (!product || this.saving() || this.adjusting() || this.needsReload() || this.loading())
      return;
    this.adjustmentForm.markAllAsTouched();
    if (this.adjustmentForm.invalid) return;
    const values = this.adjustmentForm.getRawValue();
    this.adjusting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.adjustmentErrors.set({});
    this.api
      .adjust(product.id, {
        balance: values.balance,
        reason: values.reason.trim(),
        rowVersion: product.rowVersion,
      })
      .pipe(
        finalize(() => this.adjusting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (updated) => {
          this.product.set(updated);
          this.form.controls.balance.setValue(updated.balance);
          this.adjustmentForm.reset({ balance: updated.balance, reason: '' });
          this.adjustmentOpen.set(false);
          this.success.set('Saldo atualizado.');
        },
        error: (error) => {
          this.adjustmentErrors.set(apiFieldErrors(error));
          this.handleError(error, 'Não foi possível ajustar o saldo.');
        },
      });
  }

  fieldError(field: 'code' | 'description' | 'balance'): string | null {
    const serverMessage = this.serverErrors()[field]?.[0];
    if (serverMessage) return serverMessage;
    const control = this.form.controls[field];
    if (!control.touched || !control.invalid) return null;
    if (field === 'balance') return 'Informe um saldo inteiro entre 0 e 2.147.483.647.';
    if (control.hasError('maxlength')) return `Use até ${field === 'code' ? 50 : 200} caracteres.`;
    return field === 'code' ? 'Informe o código do produto.' : 'Informe a descrição do produto.';
  }

  adjustmentFieldError(field: 'balance' | 'reason'): string | null {
    const serverMessage = this.adjustmentErrors()[field]?.[0];
    if (serverMessage) return serverMessage;
    const control = this.adjustmentForm.controls[field];
    if (!control.touched || !control.invalid) return null;
    return field === 'balance'
      ? 'Informe um saldo inteiro entre 0 e 2.147.483.647.'
      : 'Informe o motivo do ajuste, com até 200 caracteres.';
  }

  private handleError(error: unknown, fallback: string): void {
    let message = apiErrorMessage(error, fallback);
    if (error instanceof HttpErrorResponse && error.error?.code === 'ROW_VERSION_CONFLICT') {
      this.needsReload.set(true);
      message =
        'Este produto foi alterado por outra operação. Recarregue os dados e revise suas alterações antes de salvar novamente.';
    } else if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
      this.needsReload.set(!!this.id);
      this.creationUncertain.set(!this.id);
      message = this.id
        ? 'Não foi possível confirmar a alteração. Recarregue o produto para consultar o resultado antes de tentar novamente.'
        : 'Não foi possível confirmar o cadastro. Consulte a lista de produtos antes de tentar cadastrar novamente.';
    }
    this.error.set(message);
  }
}
