import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  input,
} from '@angular/core';
import {
  ArrowLeft,
  ArrowRight,
  Box,
  Check,
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  FileText,
  Pencil,
  Plus,
  Printer,
  RotateCw,
  Search,
  Trash2,
  X,
  createElement,
  IconNode,
} from 'lucide';

const icons: Record<string, IconNode> = {
  'arrow-left': ArrowLeft,
  'arrow-right': ArrowRight,
  box: Box,
  package: Box,
  file: FileText,
  plus: Plus,
  print: Printer,
  refresh: RotateCw,
  search: Search,
  close: X,
  check: Check,
  alert: CircleAlert,
  edit: Pencil,
  trash: Trash2,
  'chevron-left': ChevronLeft,
  'chevron-right': ChevronRight,
};

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
  host: { 'aria-hidden': 'true' },
  styles:
    ':host { display: inline-flex; align-items: center; flex-shrink: 0; vertical-align: middle; width: 20px; height: 20px; }',
})
export class IconComponent {
  readonly name = input('box');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    effect(() => {
      const svg = createElement(icons[this.name()] ?? Box, {
        width: 20,
        height: 20,
        'stroke-width': 1.7,
        'aria-hidden': 'true',
        focusable: 'false',
      });
      this.host.nativeElement.replaceChildren(svg);
    });
  }
}
