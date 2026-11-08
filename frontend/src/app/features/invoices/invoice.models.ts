export type InvoiceStatus = 'Open' | 'Closed';

export interface InvoiceItem {
  id: string;
  productId: string;
  productCode: string;
  productDescription: string;
  quantity: number;
}

export interface Invoice {
  id: string;
  number: number;
  status: InvoiceStatus;
  createdAt: string;
  closedAt: string | null;
  items: InvoiceItem[];
}

export interface CreateInvoice {
  items: { productId: string; quantity: number }[];
}
