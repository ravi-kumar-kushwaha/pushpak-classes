export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  page?: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ApiErrorResponse {
  success?: boolean;
  message?: string;
  statusCode?: number;
  timestampUtc?: string;
  errors?: Record<string, string[]>;
}
