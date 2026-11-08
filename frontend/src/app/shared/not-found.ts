import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageHeaderComponent } from './page-header';

@Component({
  imports: [RouterLink, PageHeaderComponent],
  template: `<div class="page">
    <app-page-header
      title="Página não encontrada"
      description="Este endereço não corresponde a uma página da aplicação."
      [count]="404"
      countLabel="página indisponível"
    /><a routerLink="/produtos" class="button button--primary">Voltar para produtos</a>
  </div>`,
})
export class NotFound {}
