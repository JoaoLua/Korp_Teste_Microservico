import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'produtos' },
  {
    path: 'produtos',
    title: 'Produtos · Korp',
    loadComponent: () => import('./features/products/product-list').then((m) => m.ProductList),
  },
  {
    path: 'produtos/novo',
    title: 'Novo produto · Korp',
    loadComponent: () => import('./features/products/product-form').then((m) => m.ProductForm),
  },
  {
    path: 'produtos/:id/editar',
    title: 'Editar produto · Korp',
    loadComponent: () => import('./features/products/product-form').then((m) => m.ProductForm),
  },
  {
    path: 'notas',
    title: 'Notas · Korp',
    loadComponent: () => import('./features/invoices/invoice-list').then((m) => m.InvoiceList),
  },
  {
    path: 'notas/nova',
    title: 'Nova nota · Korp',
    loadComponent: () => import('./features/invoices/invoice-form').then((m) => m.InvoiceForm),
  },
  {
    path: 'notas/:id',
    title: 'Nota · Korp',
    loadComponent: () => import('./features/invoices/invoice-detail').then((m) => m.InvoiceDetail),
  },
  {
    path: '**',
    title: 'Página não encontrada · Korp',
    loadComponent: () => import('./shared/not-found').then((m) => m.NotFound),
  },
];
