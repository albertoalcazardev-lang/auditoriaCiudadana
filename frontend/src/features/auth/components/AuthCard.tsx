import { ShieldCheck, type LucideIcon } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import '../login.css';

interface AuthCardProps {
  title: string;
  subtitle: string;
  description: string;
  icon: LucideIcon;
  selector?: ReactNode;
  badge?: ReactNode;
  children: ReactNode;
}

export function AuthCard({ title, subtitle, description, icon: Icon, selector, badge, children }: AuthCardProps) {
  useEffect(() => {
    const previous = document.title;
    document.title = `${title} | VyT Systems`;
    return () => { document.title = previous; };
  }, [title]);

  return (
    <main className="login-page">
      <div className="login-column">
        <header className="text-center">
          <h1>Gestión de Solicitudes</h1>
          <p className="login-subtitle">{subtitle}</p>
        </header>
        <section className="login-card" aria-labelledby="access-title">
          {selector}
          <header className="login-card-header">
            <span className="login-emblem"><Icon size={28} aria-hidden="true" /></span>
            <h2 id="access-title">{title}</h2>
            {badge}
            <p>{description}</p>
          </header>
          {children}
        </section>
        <footer className="login-footer">
          <ShieldCheck size={16} className="shrink-0" aria-hidden="true" />
          <p>Este sistema es seguro y está diseñado para la gestión eficiente de solicitudes organizacionales.</p>
        </footer>
      </div>
    </main>
  );
}
