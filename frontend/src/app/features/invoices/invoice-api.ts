import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Page } from '../../shared/models';
import { CreateInvoice, Invoice, InvoiceStatus } from './invoice.models';

@Injectable({ providedIn: 'root' })
export class InvoiceApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.billingApiUrl}/invoices`;

  list(page = 1, pageSize = 10, number?: number, status?: InvoiceStatus) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (number !== undefined) params = params.set('number', number);
    if (status) params = params.set('status', status);
    return this.http.get<Page<Invoice>>(this.url, { params });
  }

  get(id: string) {
    return this.http.get<Invoice>(`${this.url}/${encodeURIComponent(id)}`);
  }

  create(request: CreateInvoice) {
    return this.http.post<Invoice>(this.url, request);
  }

  print(id: string) {
    return this.http.post<Invoice>(`${this.url}/${encodeURIComponent(id)}/print`, null);
  }
}
