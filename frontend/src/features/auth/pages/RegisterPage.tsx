import { UserRoundPlus } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { AuthCard } from '../components/AuthCard';

export function RegisterPage() {
  const { state } = useLocation();
  const loginPath = state?.access === 'admin' ? '/admin/login' : '/consulta/login';
  return (
    <AuthCard title="Crear una cuenta" subtitle="Registro de usuario" icon={UserRoundPlus}
      description="El registro público aún no está disponible. Para obtener acceso, contacta al administrador.">
      <p className="auth-switch">¿Ya tienes una cuenta? <Link to={loginPath}>Iniciar sesión</Link></p>
    </AuthCard>
  );
}
