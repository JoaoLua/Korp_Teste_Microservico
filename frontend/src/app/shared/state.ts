import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IconComponent } from './icon';

@Component({
  selector: 'app-state',
  imports: [MatProgressSpinnerModule, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="state"
      [attr.role]="kind() === 'error' ? 'alert' : 'status'"
      [attr.aria-busy]="kind() === 'loading'"
    >
      <div class="state-symbol">
        @if (kind() === 'loading') {
          <mat-spinner diameter="28" aria-label="Carregando" />
        } @else {
          <app-icon [name]="kind() === 'error' ? 'alert' : 'box'" />
        }
      </div>
      <h2>{{ title() }}</h2>
      <p>{{ message() }}</p>
      <div class="state-actions"><ng-content /></div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .state {
      padding: 64px 32px;
      border: 1px solid var(--line);
      background: var(--surface);
    }
    .state-symbol {
      color: var(--accent);
      margin-bottom: 24px;
    }
    h2 {
      margin: 0;
      font-size: 1.6rem;
      font-weight: 500;
      letter-spacing: -0.035em;
    }
    p {
      margin: 12px 0 0;
      color: var(--muted);
      line-height: 1.7;
      max-width: 540px;
    }
    .state-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-top: 24px;
    }
    .state-actions:empty {
      display: none;
    }
    @media (max-width: 600px) {
      .state {
        padding: 40px 24px;
      }
    }
  `,
})
export class StateComponent {
  readonly kind = input<'loading' | 'empty' | 'error'>('empty');
  readonly title = input('');
  readonly message = input('');
}
