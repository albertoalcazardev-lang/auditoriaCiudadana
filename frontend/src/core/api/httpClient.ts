import { envConfig } from '../config/env';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export interface HttpRequestOptions<TBody = unknown> {
  method?: HttpMethod;
  body?: TBody;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  expectedStatus?: number;
}

export class HttpClientError extends Error {
  public readonly statusCode: number;
  public readonly details?: unknown;

  constructor(message: string, statusCode: number, details?: unknown) {
    super(message);
    this.name = 'HttpClientError';
    this.statusCode = statusCode;
    this.details = details;
  }
}

function resolveApiUrl(path: string): string {
  const trimmedBase = envConfig.apiBaseUrl.endsWith('/')
    ? envConfig.apiBaseUrl.slice(0, -1)
    : envConfig.apiBaseUrl;
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;

  return `${trimmedBase}${normalizedPath}`;
}

export async function httpRequest<TResponse, TBody = unknown>(
  path: string,
  options: HttpRequestOptions<TBody> = {},
): Promise<TResponse> {
  const { method = 'GET', body, headers = {}, signal } = options;

  const requestHeaders: Record<string, string> = {
    Accept: 'application/json',
    ...headers,
  };

  if (body !== undefined && !requestHeaders['Content-Type']) {
    requestHeaders['Content-Type'] = 'application/json';
  }

  const response = await fetch(resolveApiUrl(path), {
    method,
    credentials: 'include',
    cache: 'no-store',
    headers: requestHeaders,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    signal,
  });

  const contentType = response.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json') || contentType.includes('+json');

  if (!response.ok || (options.expectedStatus !== undefined && response.status !== options.expectedStatus)) {
    const errorPayload = isJson ? await response.json().catch(() => undefined) : undefined;
    throw new HttpClientError(
      `El servidor respondió con código HTTP ${response.status}.`,
      response.status,
      errorPayload,
    );
  }

  if (response.status === 204 || !isJson) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}
