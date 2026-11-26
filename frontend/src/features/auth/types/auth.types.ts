export type Access = 'admin' | 'consulta';

export interface LoginRequest {
  access: Access;
  identifier: string;
  password: string;
  remember: boolean;
}

export const accessRoles = { admin: 'Administrador', consulta: 'Consulta' } as const;
export interface Session {
  userId: string;
  userName: string;
  roles: string[];
}
export type LoginAdapter = (request: LoginRequest) => Promise<Session>;
export type FieldErrors = Partial<Record<'identifier' | 'password', string>>;

export interface RegistrationFields {
  fullName: string;
  username: string;
  email: string;
  password: string;
  confirmation: string;
}
