import { Link, Navigate } from 'react-router-dom';
import { authMessage } from '../hooks/authMessage';
import { useSessionViewModel } from '../hooks/useSessionViewModel';
import { loginVariants } from '../loginVariants';
import { accessRoles, type Access } from '../types/auth.types';
import { LoginPage } from './LoginPage';

export function SessionRoute({ access, login = false }: { access: Access; login?: boolean }) {
  const { session, logout } = useSessionViewModel();
  if (session.isPending) return <main role="status">Comprobando sesión…</main>;
  if (session.isError) return (
    <main><p role="alert">{authMessage(session.error)}</p>
      <button type="button" onClick={() => void session.refetch()}>Volver a comprobar la sesión</button></main>
  );
  if (!session.data) return login ? <LoginPage access={access} /> : <Navigate to={`/${access}/login`} replace />;

  const allowed = session.data.roles.includes(accessRoles[access]);
  const other = access === 'admin' ? 'consulta' : 'admin';
  if (login && allowed) return <Navigate to={loginVariants[access].destination} replace />;
  return (
    <main className="mx-auto max-w-2xl space-y-4 px-6 py-12">
      {allowed ? <>
        <h1>Panel de {accessRoles[access]}</h1>
        <p>Sesión iniciada: {session.data.userName}</p>
        {access === 'admin' && (
          <nav aria-label="Acciones de administrador">
            <Link
              to="/admin/solicitudes/nueva"
              className="inline-flex items-center rounded-lg bg-[#1D4ED8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#1E40AF]"
            >
              Nueva solicitud
            </Link>
          </nav>
        )}
      </> : <>
        <h1>Acceso denegado</h1>
        <p>Tu sesión no tiene permiso para esta sección.</p>
        {session.data.roles.includes(accessRoles[other]) &&
          <Link to={loginVariants[other].destination}>Ir a mi panel</Link>}
      </>}
      {logout.isError && <p role="alert">{authMessage(logout.error)}</p>}
      <button type="button" disabled={logout.isPending} onClick={() => logout.mutate()}>
        {logout.isPending ? 'Cerrando sesión…' : 'Cerrar sesión'}
      </button>
    </main>
  );
}
