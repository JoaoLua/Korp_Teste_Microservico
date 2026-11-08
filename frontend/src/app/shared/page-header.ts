import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-heading">
      <div class="heading-copy">
        <h1>{{ title() }}</h1>
        <p>{{ description() }}</p>
      </div>
      @if (count() !== null) {
        <div class="heading-number">
          <span>{{ count() | number }}</span
          ><small>{{ countLabel() }}</small>
        </div>
      }
      <div class="heading-actions"><ng-content select="[page-actions]" /></div>
    </header>
  `,
  styles: `
    :host {
      display: block;
    }
    .page-heading {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: start;
      gap: 22px 32px;
      padding: 16px 0 36px;
    }
    h1 {
      margin: 0;
      font-size: clamp(2.5rem, 4.6vw, 4.6rem);
      letter-spacing: -0.065em;
      line-height: 1.02;
      font-weight: 500;
    }
    p {
      color: var(--muted);
      max-width: 540px;
      font-size: 0.94rem;
      line-height: 1.6;
      margin: 18px 0 0;
    }
    .heading-number {
      border-left: 1px solid var(--line);
      padding: 0 0 4px 28px;
      color: var(--accent);
      min-width: 135px;
    }
    .heading-number span {
      display: block;
      font-size: clamp(3rem, 5.6vw, 5.5rem);
      line-height: 0.92;
      font-weight: 400;
      letter-spacing: -0.08em;
    }
    small {
      display: block;
      color: var(--muted);
      font-size: 0.75rem;
      margin-top: 14px;
    }
    .heading-actions:empty {
      display: none;
    }
    .heading-actions {
      grid-column: 1 / -1;
    }
    @media (max-width: 600px) {
      .page-heading {
        gap: 20px;
        padding-bottom: 28px;
      }
      .heading-number {
        min-width: 80px;
        padding-left: 16px;
      }
      p {
        font-size: 0.875rem;
      }
    }
    @media print {
      .heading-actions {
        display: none;
      }
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input('');
  readonly count = input<number | null>(null);
  readonly countLabel = input('registros');
}
