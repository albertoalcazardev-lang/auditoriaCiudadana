import { Eye, User } from 'lucide-react';
import type { Access } from './types/auth.types';

export const loginVariants = {
  admin: {
    label: 'Administrador',
    subtitle: 'Plataforma administrativa de solicitudes',
    title: 'Acceso para administradores',
    description: 'Acceso exclusivo para administradores del sistema. Administra y da seguimiento a todas las solicitudes.',
    icon: User,
    readOnly: false,
    destination: '/admin/dashboard',
  },
  consulta: {
    label: 'Consulta',
    subtitle: 'Plataforma de consulta de solicitudes',
    title: 'Acceso de consulta',
    description: 'Acceso de solo lectura a la información autorizada para tu cuenta.',
    icon: Eye,
    readOnly: true,
    destination: '/consulta/dashboard',
  },
} satisfies Record<Access, { label: string; subtitle: string; title: string; description: string;
  icon: typeof User; readOnly: boolean; destination: string }>;
