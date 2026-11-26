import { useLayoutEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { Access, FieldErrors, LoginAdapter } from '../types/auth.types';
import { authMessage } from './authMessage';
import { sessionKey } from './useSessionViewModel';

function validate(identifier: string, password: string): FieldErrors {
  const errors: FieldErrors = {};
  if (!identifier) errors.identifier = 'Ingresa tu usuario o correo electrónico.';
  else if (identifier.includes('@') && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(identifier)) {
    errors.identifier = 'Ingresa un correo electrónico válido.';
  }
  if (!password) errors.password = 'Ingresa tu contraseña.';
  return errors;
}

export function useLoginViewModel(access: Access, login: LoginAdapter) {
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(false);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [status, setStatus] = useState<'idle' | 'pending' | 'error'>('idle');
  const [message, setMessage] = useState('');
  const client = useQueryClient();
  const inFlight = useRef<symbol | null>(null);
  const identifierRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  useLayoutEffect(() => {
    setPassword('');
    setErrors({});
    setStatus('idle');
    // Descarta cualquier respuesta del modo anterior, incluso al volver con «Atrás».
    return () => { inFlight.current = null; };
  }, [access]);

  useLayoutEffect(() => {
    if (errors.identifier) identifierRef.current?.focus();
    else if (errors.password || status === 'error') passwordRef.current?.focus();
  }, [errors, status]);

  async function submit() {
    if (inFlight.current) return;
    const username = identifier.trim();
    const nextErrors = validate(username, password);
    setErrors(nextErrors);
    if (nextErrors.identifier || nextErrors.password) {
      setStatus('idle');
      return;
    }

    const request = Symbol();
    inFlight.current = request;
    setStatus('pending');
    try {
      const session = await login({ access, identifier: username, password, remember });
      // La cookie cambió incluso si el usuario navegó durante la petición.
      await client.cancelQueries({ queryKey: sessionKey });
      client.setQueryData(sessionKey, session);
    } catch (error) {
      if (inFlight.current === request) {
        setMessage(authMessage(error, true));
        setStatus('error');
      }
    } finally {
      if (inFlight.current === request) {
        setPassword('');
        inFlight.current = null;
      }
    }
  }

  return {
    identifier, setIdentifier, identifierRef,
    password, setPassword, passwordRef,
    remember, setRemember, errors, status, message, submit,
  };
}
