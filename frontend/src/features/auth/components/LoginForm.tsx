import { CircleAlert, LoaderCircle, Lock, Mail } from 'lucide-react';
import { useLoginViewModel } from '../hooks/useLoginViewModel';
import type { Access, LoginAdapter } from '../types/auth.types';
import { PasswordField } from './PasswordField';

export function LoginForm({ access, login }: { access: Access; login: LoginAdapter }) {
  const vm = useLoginViewModel(access, login);
  const pending = vm.status === 'pending';
  const error = vm.status === 'error' ? vm.message : null;

  return (
    <form noValidate aria-labelledby="access-title" aria-busy={pending}
      onSubmit={(event) => { event.preventDefault(); void vm.submit(); }}>
      {error && <p role="alert" className="login-banner login-banner-error">
        <CircleAlert size={16} aria-hidden="true" />{error}
      </p>}
      <fieldset className="login-fields" disabled={pending}>
        <div>
          <label htmlFor="login-identifier">Usuario o correo electrónico *</label>
          <div className="login-input-wrap">
            <Mail aria-hidden="true" />
            <input id="login-identifier" ref={vm.identifierRef} type="text" autoFocus required
              autoComplete="username" autoCapitalize="none" spellCheck={false}
              value={vm.identifier} onChange={(event) => vm.setIdentifier(event.target.value)}
              placeholder="ejemplo@empresa.com" aria-invalid={Boolean(vm.errors.identifier)}
              aria-describedby={vm.errors.identifier ? 'identifier-error' : undefined} />
          </div>
          {vm.errors.identifier && <p id="identifier-error" className="login-field-error">{vm.errors.identifier}</p>}
        </div>
        <PasswordField key={access} id="login-password" label="Contraseña" placeholder="Ingresa tu contraseña"
          autoComplete="current-password" inputRef={vm.passwordRef} value={vm.password} onChange={vm.setPassword}
          error={vm.errors.password} />
        <div className="login-options">
          <label className="login-remember">
            <input type="checkbox" checked={vm.remember}
              onChange={(event) => vm.setRemember(event.target.checked)} />
            Recordar mi sesión
          </label>
          <details className="login-recovery">
            <summary>¿Olvidaste tu contraseña?</summary>
            <p>Contacta al administrador para recuperar el acceso.</p>
          </details>
        </div>
        <button type="submit" className="login-submit">
          {pending ? <LoaderCircle className="login-spinner" size={16} aria-hidden="true" />
            : <Lock size={16} aria-hidden="true" />}
          {pending ? 'Iniciando sesión…' : 'Iniciar sesión'}
        </button>
      </fieldset>
    </form>
  );
}
