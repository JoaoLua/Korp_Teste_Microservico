import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, unknown>;
}

function problemFrom(error: unknown): ProblemDetails {
  if (!(error instanceof HttpErrorResponse) || !error.error || typeof error.error !== 'object') {
    return {};
  }
  return error.error as ProblemDetails;
}

export function apiErrorMessage(
  error: unknown,
  fallback = 'Não foi possível concluir a operação. Tente novamente.',
) {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'Não foi possível conectar ao serviço. Verifique a conexão e tente novamente.';
  }
  const problem = problemFrom(error);
  if (typeof problem.detail === 'string' && problem.detail.trim()) return problem.detail;
  const fields = Object.values(apiFieldErrors(error)).flat();
  if (fields.length) return fields.join(' ');
  if (
    typeof problem.title === 'string' &&
    problem.title.trim() &&
    !/^[A-Z_]+$/.test(problem.title)
  ) {
    return problem.title;
  }
  return fallback;
}

export function apiFieldErrors(error: unknown): Record<string, string[]> {
  const entries = problemFrom(error).errors;
  if (!entries || typeof entries !== 'object') return {};
  const result: Record<string, string[]> = {};
  for (const [key, value] of Object.entries(entries)) {
    if (!Array.isArray(value)) continue;
    const field = key.replace(/^\$\./, '');
    const normalized = field.charAt(0).toLowerCase() + field.slice(1);
    result[normalized] = value.filter((message): message is string => typeof message === 'string');
  }
  return result;
}
