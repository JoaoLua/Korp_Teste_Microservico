import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Page } from '../../shared/models';
import { AdjustStock, CreateProduct, Product, UpdateProduct } from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.stockApiUrl}/products`;

  list(search = '', page = 1, pageSize = 10) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<Page<Product>>(this.url, { params });
  }

  get(id: string) {
    return this.http.get<Product>(`${this.url}/${encodeURIComponent(id)}`);
  }

  create(request: CreateProduct) {
    return this.http.post<Product>(this.url, request);
  }

  improveDescription(description: string) {
    return this.http.post<{ description: string }>(`${this.url}/improve-description`, {
      description,
    });
  }

  update(id: string, request: UpdateProduct) {
    return this.http.put<Product>(`${this.url}/${encodeURIComponent(id)}`, request);
  }

  adjust(id: string, request: AdjustStock) {
    return this.http.post<Product>(`${this.url}/${encodeURIComponent(id)}/adjustments`, request);
  }
}
