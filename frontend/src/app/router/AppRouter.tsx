import { BrowserRouter, Link, Navigate, Route, Routes } from 'react-router-dom';
import { SessionRoute } from '../../features/auth/pages/SessionRoute';
import { RegisterPage } from '../../features/auth/pages/RegisterPage';
import { NuevaSolicitudPage } from '../../features/solicitudes/pages/NuevaSolicitudPage';

function BaseWelcomeView() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center px-6 py-12">
      <h1 className="mt-2 text-3xl font-bold tracking-tight text-white sm:text-4xl">
        VyT Systems
      </h1>
      <p className="mt-3 text-sm leading-relaxed text-slate-400">
        Selecciona el acceso correspondiente a tu cuenta.
      </p>
      <nav aria-label="Iniciar sesión" className="mt-4 flex gap-6 underline">
        <Link to="/admin/login">Administrador</Link>
        <Link to="/consulta/login">Consulta</Link>
      </nav>
    </main>
  );
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<BaseWelcomeView />} />
        <Route path="/admin/login" element={<SessionRoute access="admin" login />} />
        <Route path="/consulta/login" element={<SessionRoute access="consulta" login />} />
        <Route path="/admin/dashboard" element={<SessionRoute access="admin" />} />
        <Route path="/admin/solicitudes/nueva" element={<NuevaSolicitudPage />} />
        <Route path="/consulta/dashboard" element={<SessionRoute access="consulta" />} />
        <Route path="/registro" element={<RegisterPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
