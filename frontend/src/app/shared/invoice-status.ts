import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { InvoiceStatus } from '../features/invoices/invoice.models';

@Component({
  selector: 'app-invoice-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-badge" [class.is-closed]="status() === 'Closed'">
    <span class="status-dot" aria-hidden="true"></span
    >{{ status() === 'Open' ? 'Aberta' : 'Fechada' }}
  </span>`,
  styles: `
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: var(--accent);
      border: 1px solid var(--accent);
      padding: 6px 10px;
      font-size: 0.75rem;
      white-space: nowrap;
      line-height: 1.2;
    }
    .status-dot {
      width: 5px;
      height: 5px;
      background: currentColor;
      border-radius: 50%;
    }
    .is-closed {
      color: var(--muted);
      border-color: var(--line);
      background: var(--surface);
    }
  `,
})
export class InvoiceStatusComponent {
  readonly status = input.required<InvoiceStatus>();
}
