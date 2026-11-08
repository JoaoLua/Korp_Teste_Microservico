export interface Product {
  id: string;
  code: string;
  description: string;
  balance: number;
  minimumStock: number;
  category: string | null;
  createdAt: string;
  updatedAt: string | null;
  rowVersion: string;
}

export interface CreateProduct {
  code: string;
  description: string;
  balance: number;
  minimumStock?: number;
  category?: string | null;
}

export interface UpdateProduct {
  code: string;
  description: string;
  rowVersion: string;
  minimumStock?: number;
  category?: string | null;
}

export interface AdjustStock {
  balance: number;
  reason: string;
  rowVersion: string;
}
