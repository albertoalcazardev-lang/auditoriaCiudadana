import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import {
  BarChart3,
  Building2,
  ClipboardList,
  FileCheck2,
  FileText,
  FolderOpen,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  TrendingUp,
  User,
  Users,
  X,
} from 'lucide-react';
import { authMessage } from '../../features/auth/hooks/authMessage';
import type { Session } from '../../features/auth/types/auth.types';
import './adminShell.css';

interface LogoutAction {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  mutate: () => void;
}

interface AdminShellProps {
  session: Session;
  title: string;
  breadcrumbParent: string;
  breadcrumbCurrent: string;
  onNavigateDashboard: () => void;
  onNavigateSolicitudes?: () => void;
  logout: LogoutAction;
  children: ReactNode;
}

const DISABLED_NAV_ITEMS = [
  { label: 'Obras', icon: Building2 },
  { label: 'Contratos', icon: FileCheck2 },
  { label: 'Avances', icon: TrendingUp },
  { label: 'Reportes', icon: BarChart3 },
  { label: 'Documentos', icon: FolderOpen },
  { label: 'Usuarios', icon: Users },
  { label: 'Configuración', icon: Settings },
] as const;

const DRAWER_FOCUSABLE_SELECTOR =
  'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])';

export function AdminShell({
  session,
  title,
  breadcrumbParent,
  breadcrumbCurrent,
  onNavigateDashboard,
  onNavigateSolicitudes,
  logout,
  children,
}: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const sidebarRef = useRef<HTMLElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const menuTriggerRef = useRef<HTMLButtonElement | null>(null);

  const closeDrawer = useCallback((restoreFocus = true) => {
    setDrawerOpen(false);
    if (restoreFocus) {
      requestAnimationFrame(() => {
        menuTriggerRef.current?.focus();
      });
    }
  }, []);

  useEffect(() => {
    if (!drawerOpen) return;

    const frameId = requestAnimationFrame(() => {
      if (closeButtonRef.current && closeButtonRef.current.offsetParent !== null) {
        closeButtonRef.current.focus();
      } else {
        const firstFocusable =
          sidebarRef.current?.querySelector<HTMLElement>(DRAWER_FOCUSABLE_SELECTOR);
        firstFocusable?.focus();
      }
    });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeDrawer(true);
        return;
      }

      if (event.key !== 'Tab' || !sidebarRef.current) return;

      const focusable = Array.from(
        sidebarRef.current.querySelectorAll<HTMLElement>(DRAWER_FOCUSABLE_SELECTOR),
      ).filter((el) => el.offsetParent !== null);
      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement as HTMLElement | null;

      if (event.shiftKey) {
        if (!active || active === first || !sidebarRef.current.contains(active)) {
          event.preventDefault();
          last.focus();
        }
      } else if (!active || active === last || !sidebarRef.current.contains(active)) {
        event.preventDefault();
        first.focus();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      cancelAnimationFrame(frameId);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [closeDrawer, drawerOpen]);

  const handleDashboardClick = () => {
    closeDrawer(false);
    onNavigateDashboard();
  };

  return (
    <div className="admin-shell">
      <div
        className={`admin-backdrop ${drawerOpen ? 'is-open' : ''}`}
        aria-hidden="true"
        onClick={() => closeDrawer(true)}
      />

      <aside
        ref={sidebarRef}
        id="admin-sidebar-nav"
        className={`admin-sidebar ${drawerOpen ? 'is-open' : ''}`}
        aria-label="Navegación administrativa"
      >
        <div className="admin-sidebar-header">
          <div className="admin-brand">
            <span className="admin-brand-icon" aria-hidden="true">
              <FileText size={18} />
            </span>
            <span className="admin-brand-text">Gestión de Solicitudes</span>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            className="admin-sidebar-close"
            aria-label="Cerrar menú lateral"
            onClick={() => closeDrawer(true)}
          >
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        <nav className="admin-nav" aria-label="Menú principal">
          <button
            type="button"
            className="admin-nav-item"
            title="Dashboard"
            onClick={handleDashboardClick}
          >
            <LayoutDashboard size={18} aria-hidden="true" />
            <span className="admin-nav-label">Dashboard</span>
          </button>

          <button
            type="button"
            className="admin-nav-item"
            aria-current="page"
            title="Solicitudes"
            onClick={() => {
              if (onNavigateSolicitudes) {
                closeDrawer(false);
                onNavigateSolicitudes();
              } else if (drawerOpen) {
                closeDrawer(true);
              }
            }}
          >
            <ClipboardList size={18} aria-hidden="true" />
            <span className="admin-nav-label">Solicitudes</span>
          </button>

          {DISABLED_NAV_ITEMS.map((item) => {
            const Icon = item.icon;
            return (
              <button
                key={item.label}
                type="button"
                className="admin-nav-item"
                disabled
                aria-disabled="true"
                title={item.label}
              >
                <Icon size={18} aria-hidden="true" />
                <span className="admin-nav-label">{item.label}</span>
              </button>
            );
          })}
        </nav>

        <div className="admin-sidebar-footer">
          {logout.isError && (
            <p role="alert" className="admin-logout-error">
              {authMessage(logout.error)}
            </p>
          )}
          <button
            type="button"
            className="admin-logout-btn"
            disabled={logout.isPending}
            title="Cerrar sesión"
            onClick={() => logout.mutate()}
          >
            <LogOut size={18} aria-hidden="true" />
            <span className="admin-nav-label">
              {logout.isPending ? 'Cerrando sesión…' : 'Cerrar sesión'}
            </span>
          </button>
        </div>
      </aside>

      <div className="admin-main-wrapper" inert={drawerOpen}>
        <header className="admin-topbar">
          <div className="admin-topbar-left">
            <button
              ref={menuTriggerRef}
              type="button"
              className="admin-menu-trigger"
              aria-label="Abrir menú lateral"
              aria-expanded={drawerOpen}
              aria-controls="admin-sidebar-nav"
              onClick={() => setDrawerOpen(true)}
            >
              <Menu size={20} aria-hidden="true" />
            </button>

            <div className="admin-topbar-heading">
              <h1 className="admin-page-title">{title}</h1>
              <nav aria-label="Breadcrumb" className="admin-breadcrumb">
                <span>{breadcrumbParent}</span>
                <span aria-hidden="true">›</span>
                <span className="admin-breadcrumb-current">{breadcrumbCurrent}</span>
              </nav>
            </div>
          </div>

          <div className="admin-user-chip">
            <span className="admin-user-avatar" aria-hidden="true">
              <User size={17} />
            </span>
            <div className="admin-user-meta">
              <span className="admin-user-role">Administrador</span>
              <span className="admin-user-name">{session.userName}</span>
            </div>
          </div>
        </header>

        <main className="admin-content">{children}</main>
      </div>
    </div>
  );
}
