import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';

function BaseWelcomeView() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center px-6 py-12">
      <p className="text-xs font-semibold tracking-widest text-indigo-400 uppercase">
        Solución Base · Task 00
      </p>
      <h1 className="mt-2 text-3xl font-bold tracking-tight text-white sm:text-4xl">
        VyT Systems
      </h1>
      <p className="mt-3 text-sm leading-relaxed text-slate-400">
        Esqueleto arquitectónico preparado para el desarrollo colaborativo de módulos bajo Clean
        Architecture, Vertical Slices, CQRS y MVVM.
      </p>
    </main>
  );
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<BaseWelcomeView />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
