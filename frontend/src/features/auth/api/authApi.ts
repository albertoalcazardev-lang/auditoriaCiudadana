import { httpRequest, HttpClientError } from '../../../core/api/httpClient';
import { accessRoles, type LoginAdapter, type Session } from '../types/auth.types';

let csrfToken: string | undefined;

async function refreshCsrf() {
  csrfToken = undefined;
  const result = await httpRequest<{ requestToken: string }>('/auth/csrf');
  if (!result?.requestToken) throw new Error('Respuesta CSRF incompleta.');
  return csrfToken = result.requestToken;
}

export async function getCsrfToken(): Promise<string> {
  return csrfToken ?? (await refreshCsrf());
}

export function clearCsrfToken(): void {
  csrfToken = undefined;
}

async function readSession(signal?: AbortSignal) {
  const session = await httpRequest<Session>('/auth/me', { signal });
  if (!session?.userId || !session.userName || !Array.isArray(session.roles))
    throw new Error('Respuesta de sesión incompleta.');
  return session;
}

export async function restoreSession(signal?: AbortSignal) {
  try {
    return await readSession(signal);
  } catch (error) {
    if (error instanceof HttpClientError && error.statusCode === 401) {
      csrfToken = undefined;
      return null;
    }
    throw error;
  }
}

export const submitLogin: LoginAdapter = async ({ access, identifier, password, remember }) => {
  await httpRequest<void>('/auth/login', {
    method: 'POST', expectedStatus: 200, headers: { 'X-CSRF-TOKEN': await refreshCsrf() },
    body: { identifier, password, rememberSession: remember, requestedAccess: accessRoles[access] },
  });
  // El token anónimo ya no corresponde a la identidad de la cookie.
  await refreshCsrf();
  return readSession();
};

export async function signOut() {
  try {
    await httpRequest<void>('/auth/logout', {
      method: 'POST', expectedStatus: 204, headers: { 'X-CSRF-TOKEN': await getCsrfToken() },
    });
  } finally {
    // También ante un 400: el siguiente intento solicitará un token fresco.
    clearCsrfToken();
  }
}
