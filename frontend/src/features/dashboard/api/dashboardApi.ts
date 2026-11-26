import { httpRequest } from '../../../core/api/httpClient';
import type { DashboardResponse, PeriodoDashboard } from '../types/dashboard.types';

export async function obtenerDashboard(
  periodo: PeriodoDashboard,
  signal?: AbortSignal,
): Promise<DashboardResponse> {
  return httpRequest<DashboardResponse>(
    `/dashboard?periodo=${encodeURIComponent(periodo)}`,
    { signal },
  );
}
