export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PaginationChange {
  page: number;
  pageSize: number;
}
