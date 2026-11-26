import { Eye } from 'lucide-react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { submitLogin } from '../api/authApi';
import { AuthCard } from '../components/AuthCard';
import { LoginForm } from '../components/LoginForm';
import { loginVariants } from '../loginVariants';
import type { Access } from '../types/auth.types';

export function LoginPage({ access }: { access: Access }) {
  const variant = loginVariants[access];
  const { search } = useLocation();
  return (
    <AuthCard {...variant}
      selector={
        <nav aria-label="Tipo de acceso" className="login-selector">
          {Object.entries(loginVariants).map(([mode, { label }]) => (
            <NavLink key={mode} to={{ pathname: `/${mode}/login`, search }} end>{label}</NavLink>
          ))}
        </nav>
      }
      badge={variant.readOnly && <span className="login-badge"><Eye size={14} aria-hidden="true" /> Solo lectura</span>}>
      <LoginForm access={access} login={submitLogin} />
      <p className="auth-switch"><Link to="/registro" state={{ access }}>Información sobre el registro</Link></p>
    </AuthCard>
  );
}
