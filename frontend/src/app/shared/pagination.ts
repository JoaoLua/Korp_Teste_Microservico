import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { PaginationChange } from './models';

export function portuguesePaginator() {
  const labels = new MatPaginatorIntl();
  labels.itemsPerPageLabel = 'Por página';
  labels.nextPageLabel = 'Próxima página';
  labels.previousPageLabel = 'Página anterior';
  labels.firstPageLabel = 'Primeira página';
  labels.lastPageLabel = 'Última página';
  labels.getRangeLabel = (page, size, total) =>
    total === 0
      ? '0 registros'
      : `${page * size + 1}–${Math.min((page + 1) * size, total)} de ${total}`;
  return labels;
}

@Component({
  selector: 'app-pagination',
  imports: [MatPaginatorModule],
  providers: [{ provide: MatPaginatorIntl, useFactory: portuguesePaginator }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<mat-paginator
    [length]="total()"
    [pageIndex]="page() - 1"
    [pageSize]="pageSize()"
    [pageSizeOptions]="[10, 20, 50]"
    [disabled]="disabled()"
    (page)="change($event)"
    aria-label="Paginação"
  />`,
  styles: `
    :host {
      display: block;
      border-top: 1px solid var(--line);
    }
  `,
})
export class PaginationComponent {
  readonly page = input(1);
  readonly pageSize = input(10);
  readonly total = input(0);
  readonly disabled = input(false);
  readonly pageChange = output<PaginationChange>();

  change(event: PageEvent) {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
