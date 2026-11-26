import { expect, test, type Page, type Route } from '@playwright/test';
import { randomUUID } from 'node:crypto';

if (process.env.PLAYWRIGHT_BASE_URL) {
  test.use({ baseURL: process.env.PLAYWRIGHT_BASE_URL });
}

interface BrowserSpyGlobal {
  __pushedUrls?: string[];
  history: {
    pushState: (data: unknown, unused: string, url?: string | URL | null) => void;
  };
  innerWidth: number;
  document: {
    documentElement: {
      scrollWidth: number;
    };
  };
}

async function installPushStateSpy(page: Page) {
  await page.addInitScript(() => {
    const g = globalThis as unknown as BrowserSpyGlobal;
    const pushed: string[] = [];
    g.__pushedUrls = pushed;
    const origPush = g.history.pushState.bind(g.history);
    g.history.pushState = (data: unknown, unused: string, url?: string | URL | null) => {
      if (url != null) pushed.push(String(url));
      origPush(data, unused, url);
    };
  });
}

async function readPushedUrls(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const g = globalThis as unknown as BrowserSpyGlobal;
    return g.__pushedUrls ?? [];
  });
}

interface MockDashboardPayload {
  periodo: string;
  fechaReferencia: string;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  actualizadoEnUtc: string;
  disponibilidad?: {
    indicadores?: boolean;
    porArea?: boolean;
    proximosVencimientos?: boolean;
    actividadReciente?: boolean;
  };
  indicadores: {
    total: number;
    enProceso: number;
    entregadas: number;
    vencidas: number;
    proximasAVencer: number;
    variacionTotalPorcentaje?: number | null;
  } | null;
  porArea: Array<{ area: string; cantidad: number }> | null;
  proximosVencimientos: Array<{
    folio: string;
    asunto: string;
    fechaVencimiento: string;
    diasRestantes: number;
  }> | null;
  actividadReciente: Array<{
    tipo: string;
    descripcion: string;
    folio: string;
    ocurridoEnUtc: string;
  }> | null;
}

function buildFullDashboardMock(
  overrides: Partial<MockDashboardPayload> = {},
): MockDashboardPayload {
  return {
    periodo: 'este-mes',
    fechaReferencia: '2026-11-26',
    fechaInicio: '2026-11-01',
    fechaFin: '2026-11-30',
    // 18:35 UTC = 12:35 America/Mexico_City (UTC-6)
    actualizadoEnUtc: '2026-11-26T18:35:00Z',
    disponibilidad: {
      indicadores: true,
      porArea: true,
      proximosVencimientos: true,
      actividadReciente: true,
    },
    indicadores: {
      total: 40,
      enProceso: 14,
      entregadas: 20,
      vencidas: 6,
      proximasAVencer: 4,
      variacionTotalPorcentaje: 12.5,
    },
    // 8 áreas desordenadas cuya suma = 4 + 12 + 3 + 9 + 5 + 2 + 4 + 1 = 40
    porArea: [
      { area: 'Contraloría Municipal', cantidad: 4 },
      { area: 'Dirección General de Desarrollo Urbano y Obras Públicas', cantidad: 12 },
      { area: 'Instituto Municipal de Planeación', cantidad: 3 },
      { area: 'Secretaría de Finanzas y Tesorería', cantidad: 9 },
      { area: 'Dirección de Servicios Públicos', cantidad: 5 },
      { area: 'Dirección de Ecología', cantidad: 2 },
      { area: 'Secretaría del Ayuntamiento', cantidad: 4 },
      { area: 'Archivo Histórico Municipal', cantidad: 1 },
    ],
    proximosVencimientos: [
      {
        folio: '00045-A/2026',
        asunto: 'Solicitud de contratos de pavimentación hidráulica zona norte',
        fechaVencimiento: '2026-11-27',
        diasRestantes: 1,
      },
      {
        folio: 'SOL-2026-0102',
        asunto: 'Informe trimestral de egresos y partidas presupuestales',
        fechaVencimiento: '2026-11-29',
        diasRestantes: 3,
      },
      {
        folio: 'SOL-2026-0103',
        asunto: 'Bitácora de mantenimiento de alumbrado público',
        fechaVencimiento: '2026-12-02',
        diasRestantes: 6,
      },
    ],
    actividadReciente: [
      {
        tipo: 'NuevaSolicitud',
        descripcion: 'Nueva solicitud registrada',
        folio: 'SOL-2026-0110',
        ocurridoEnUtc: '2026-11-26T18:34:40Z', // hace unos instantes (20s)
      },
      {
        tipo: 'Actualizacion',
        descripcion: 'Solicitud actualizada',
        folio: 'SOL-2026-0108',
        ocurridoEnUtc: '2026-11-26T18:20:00Z', // hace 15 min
      },
      {
        tipo: 'Entrega',
        descripcion: 'Solicitud marcada como entregada',
        folio: 'SOL-2026-0099',
        ocurridoEnUtc: '2026-11-26T16:35:00Z', // hace 2 h
      },
      {
        tipo: 'NuevaSolicitud',
        descripcion: 'Nueva solicitud registrada',
        folio: 'SOL-2026-0091',
        ocurridoEnUtc: '2026-11-23T18:35:00Z', // hace 3 d
      },
    ],
    ...overrides,
  };
}

function buildEmptyDashboardMock(periodo = 'este-mes'): MockDashboardPayload {
  return {
    periodo,
    fechaReferencia: '2026-11-26',
    fechaInicio: periodo === 'todo' ? null : '2026-11-01',
    fechaFin: periodo === 'todo' ? null : '2026-11-30',
    actualizadoEnUtc: '2026-11-26T18:35:00Z',
    disponibilidad: {
      indicadores: true,
      porArea: true,
      proximosVencimientos: true,
      actividadReciente: true,
    },
    indicadores: {
      total: 0,
      enProceso: 0,
      entregadas: 0,
      vencidas: 0,
      proximasAVencer: 0,
      variacionTotalPorcentaje: null,
    },
    porArea: [],
    proximosVencimientos: [],
    actividadReciente: [],
  };
}

async function setupDashboardRoutes(
  page: Page,
  options: {
    roles?: string[] | null;
    userName?: string;
    onMe?: (route: Route) => Promise<void> | void;
    onDashboard?: (
      route: Route,
      periodo: string | null,
      callCount: number,
    ) => Promise<void> | void;
  } = {},
) {
  const {
    roles = ['Administrador'],
    userName = 'Ana Administradora',
    onMe,
    onDashboard,
  } = options;

  let csrfToken = randomUUID();
  let signedOut = false;
  let dashboardCalls = 0;

  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (!path.startsWith('/api/')) {
      return route.continue();
    }

    if (path === '/api/auth/me' && request.method() === 'GET') {
      if (onMe) return onMe(route);
      if (!roles || signedOut) {
        return route.fulfill({ status: 401 });
      }
      return route.fulfill({
        json: { userId: 'admin-01', userName, roles },
      });
    }

    if (path === '/api/auth/csrf' && request.method() === 'GET') {
      csrfToken = randomUUID();
      return route.fulfill({ json: { requestToken: csrfToken } });
    }

    if (path === '/api/auth/logout' && request.method() === 'POST') {
      const headerToken = request.headers()['x-csrf-token'];
      if (!headerToken || headerToken !== csrfToken) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }
      signedOut = true;
      return route.fulfill({ status: 204 });
    }

    if (path === '/api/dashboard' && request.method() === 'GET') {
      dashboardCalls += 1;
      const periodo = url.searchParams.get('periodo');
      if (onDashboard) {
        return onDashboard(route, periodo, dashboardCalls);
      }
      return route.fulfill({
        json: buildFullDashboardMock({ periodo: periodo ?? 'este-mes' }),
      });
    }

    return route.abort();
  });
}

test('1. protección de ruta: espera /me, redirige anónimo a /admin/login, bloquea Consulta sin pedir /api/dashboard y permite Administrador con usuario real y logout', async ({
  page,
}) => {
  // 1a. Mientras /auth/me está pendiente, no dispara /api/dashboard
  let releaseMe!: () => void;
  const meGate = new Promise<void>((resolve) => {
    releaseMe = resolve;
  });
  let dashboardCalledWhilePending = false;

  await setupDashboardRoutes(page, {
    onMe: async (route) => {
      await meGate;
      return route.fulfill({ status: 401 });
    },
    onDashboard: (route) => {
      dashboardCalledWhilePending = true;
      return route.fulfill({ json: buildFullDashboardMock() });
    },
  });

  await page.goto('/admin/dashboard');
  await expect(page.getByRole('status')).toContainText('Comprobando sesión…');
  expect(dashboardCalledWhilePending).toBe(false);
  releaseMe();
  await expect(page).toHaveURL(/\/admin\/login$/);
  expect(dashboardCalledWhilePending).toBe(false);

  // 1b. Rol Consulta recibe Acceso denegado y no invoca /api/dashboard
  await page.unrouteAll();
  let dashboardCalledByConsulta = false;
  await setupDashboardRoutes(page, {
    roles: ['Consulta'],
    onDashboard: (route) => {
      dashboardCalledByConsulta = true;
      return route.fulfill({ json: buildFullDashboardMock() });
    },
  });

  await page.goto('/admin/dashboard');
  await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
  await expect(page.getByText('Tu sesión no tiene permiso para esta sección.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Ir a mi panel' })).toHaveAttribute(
    'href',
    '/consulta/dashboard',
  );
  expect(dashboardCalledByConsulta).toBe(false);

  // 1c. Rol Administrador accede al Dashboard con AdminShell, subtítulo, usuario real y logout
  await page.unrouteAll();
  await setupDashboardRoutes(page, {
    roles: ['Administrador'],
    userName: 'Laura Méndez',
  });

  await page.goto('/admin/dashboard');
  await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();
  await expect(page.getByText('Resumen general de las solicitudes')).toBeVisible();
  await expect(page.getByText('Laura Méndez')).toBeVisible();

  const dashboardNavBtn = page.getByRole('button', { name: 'Dashboard' });
  const solicitudesNavBtn = page.getByRole('button', { name: 'Solicitudes' });
  await expect(dashboardNavBtn).toHaveAttribute('aria-current', 'page');
  await expect(solicitudesNavBtn).not.toHaveAttribute('aria-current', 'page');

  await page.getByRole('button', { name: 'Cerrar sesión' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);
});

test('2 y 3. cinco KPIs reconciliados, hora en America/Mexico_City y Donut de 3 segmentos con leyenda y tooltip accesible', async ({
  page,
}) => {
  await setupDashboardRoutes(page);
  await page.goto('/admin/dashboard');

  // Hora actualizada 18:35Z -> 12:35 America/Mexico_City
  await expect(page.getByTestId('dashboard-updated-status')).toHaveText('Actualizado a las 12:35');

  // 5 KPIs con valores reales y líneas secundarias
  await expect(page.getByTestId('kpi-value-total')).toHaveText('40');
  await expect(page.getByTestId('kpi-secondary-total')).toHaveText('+12.5% vs. periodo anterior');

  await expect(page.getByTestId('kpi-value-en-proceso')).toHaveText('14');
  await expect(page.getByTestId('kpi-secondary-en-proceso')).toHaveText('35% del total');

  await expect(page.getByTestId('kpi-value-entregadas')).toHaveText('20');
  await expect(page.getByTestId('kpi-secondary-entregadas')).toHaveText('50% del total');

  await expect(page.getByTestId('kpi-value-vencidas')).toHaveText('6');
  await expect(page.getByTestId('kpi-secondary-vencidas')).toHaveText('15% del total');

  await expect(page.getByTestId('kpi-value-proximas-a-vencer')).toHaveText('4');
  await expect(page.getByTestId('kpi-secondary-proximas-a-vencer')).toHaveText('Próximos 7 días');

  // Reconciliación matemática: Total = En proceso + Entregadas + Vencidas
  const totalNum = Number(await page.getByTestId('kpi-value-total').innerText());
  const enProcesoNum = Number(await page.getByTestId('kpi-value-en-proceso').innerText());
  const entregadasNum = Number(await page.getByTestId('kpi-value-entregadas').innerText());
  const vencidasNum = Number(await page.getByTestId('kpi-value-vencidas').innerText());
  expect(enProcesoNum + entregadasNum + vencidasNum).toBe(totalNum);

  // PeriodSelect 160x40px
  const periodBox = await page.getByLabel('Periodo').boundingBox();
  expect(Math.round(periodBox!.width)).toBe(160);
  expect(Math.round(periodBox!.height)).toBe(40);

  // Donut SVG 180x180 con role="group", total central y 3 segmentos mutuamente excluyentes sin role="button"
  const donutSvg = page.getByTestId('donut-svg');
  await expect(donutSvg).toBeVisible();
  await expect(donutSvg).toHaveAttribute('role', 'group');
  const svgBox = await donutSvg.boundingBox();
  expect(Math.round(svgBox!.width)).toBe(180);
  expect(Math.round(svgBox!.height)).toBe(180);

  await expect(page.getByTestId('donut-center-total')).toHaveText('40');
  await expect(page.getByTestId('donut-legend-en-proceso')).toContainText('En proceso');
  await expect(page.getByTestId('donut-legend-en-proceso')).toContainText('14');
  await expect(page.getByTestId('donut-legend-en-proceso')).toContainText('(35%)');
  await expect(page.getByTestId('donut-legend-entregadas')).toContainText('20');
  await expect(page.getByTestId('donut-legend-entregadas')).toContainText('(50%)');
  await expect(page.getByTestId('donut-legend-vencidas')).toContainText('6');
  await expect(page.getByTestId('donut-legend-vencidas')).toContainText('(15%)');

  // Tooltip por hover y por foco de teclado en segmentos del Donut sin desplazar layout
  const legendBefore = await page.getByTestId('donut-legend-en-proceso').boundingBox();
  const segEnProceso = page.getByTestId('donut-segment-en-proceso');
  await expect(segEnProceso).not.toHaveAttribute('role', 'button');
  await segEnProceso.focus();
  await expect(page.getByTestId('donut-tooltip')).toContainText('En proceso: 14 (35%)');
  const legendDuring = await page.getByTestId('donut-legend-en-proceso').boundingBox();
  expect(Math.round(legendDuring!.y)).toBe(Math.round(legendBefore!.y));

  const segVencidas = page.getByTestId('donut-segment-vencidas');
  await segVencidas.hover();
  await expect(page.getByTestId('donut-tooltip')).toContainText('Vencidas: 6 (15%)');
});

test('4, 5 y 6. barras por área (top 6 + Otras = Total y tooltip completo), tabla de próximos vencimientos con navegación y actividad reciente relativa', async ({
  page,
}) => {
  await installPushStateSpy(page);

  await setupDashboardRoutes(page, {
    onDashboard: (route, periodo) => {
      if (periodo === 'todo') {
        return route.fulfill({
          json: buildFullDashboardMock({
            periodo: 'todo',
            fechaInicio: null,
            fechaFin: null,
          }),
        });
      }
      return route.fulfill({ json: buildFullDashboardMock({ periodo: periodo ?? 'este-mes' }) });
    },
  });

  await page.goto('/admin/dashboard');

  // 4. Gráfica de barras por área: 8 áreas se agrupan en 6 principales ordenadas descendentemente + "Otras"
  await expect(page.getByTestId('area-guide-line')).toHaveCount(4);

  const expectedOrderedAreas = [
    { area: 'Dirección General de Desarrollo Urbano y Obras Públicas', cantidad: 12 },
    { area: 'Secretaría de Finanzas y Tesorería', cantidad: 9 },
    { area: 'Dirección de Servicios Públicos', cantidad: 5 },
    { area: 'Contraloría Municipal', cantidad: 4 },
    { area: 'Secretaría del Ayuntamiento', cantidad: 4 },
    { area: 'Instituto Municipal de Planeación', cantidad: 3 },
    { area: 'Otras', cantidad: 3 }, // 2 (Ecología) + 1 (Archivo Histórico) = 3
  ];

  let barsTotal = 0;
  for (let i = 0; i < expectedOrderedAreas.length; i++) {
    const bar = page.getByTestId(`area-bar-${i}`);
    await expect(bar).toHaveAttribute('data-area', expectedOrderedAreas[i].area);
    await expect(bar).toHaveAttribute('data-count', String(expectedOrderedAreas[i].cantidad));
    const barBox = await bar.boundingBox();
    expect(Math.round(barBox!.width)).toBe(32);
    barsTotal += expectedOrderedAreas[i].cantidad;
  }
  expect(barsTotal).toBe(40);
  await expect(page.getByTestId('area-bar-7')).toHaveCount(0);

  // Tooltip con nombre completo del área receptora y cantidad sin salto de layout
  await page.getByTestId('area-bar-0').scrollIntoViewIfNeeded();
  const cardBefore = await page.getByTestId('widget-area-chart').boundingBox();
  const bar0Before = await page.getByTestId('area-bar-0').boundingBox();
  await page.getByTestId('area-bar-0').hover();
  await expect(page.getByTestId('area-chart-tooltip')).toContainText(
    'Dirección General de Desarrollo Urbano y Obras Públicas: 12 solicitudes',
  );
  const cardDuring = await page.getByTestId('widget-area-chart').boundingBox();
  const bar0During = await page.getByTestId('area-bar-0').boundingBox();
  expect(Math.round(bar0During!.y - cardDuring!.y)).toBe(
    Math.round(bar0Before!.y - cardBefore!.y),
  );
  expect(Math.round(bar0During!.height)).toBe(Math.round(bar0Before!.height));

  // 6. Actividad reciente verificable y tiempo relativo respecto a actualizadoEnUtc
  const activityWidget = page.getByTestId('widget-recent-activity');
  await expect(activityWidget).toContainText('SOL-2026-0110');
  await expect(activityWidget).toContainText('hace unos instantes');
  await expect(activityWidget).toContainText('SOL-2026-0108');
  await expect(activityWidget).toContainText('hace 15 min');
  await expect(activityWidget).toContainText('SOL-2026-0099');
  await expect(activityWidget).toContainText('hace 2 h');
  await expect(activityWidget).toContainText('SOL-2026-0091');
  await expect(activityWidget).toContainText('hace 3 d');

  // 5. Tabla de próximos vencimientos: dd/mm/aaaa, chips de días y enlace Ver todas
  const row1 = page.getByTestId('deadline-row-00045-A/2026');
  await expect(row1).toContainText('27/11/2026');
  await expect(page.getByTestId('deadline-chip-00045-A/2026')).toHaveText('1 día');
  await expect(page.getByTestId('deadline-chip-00045-A/2026')).toHaveClass(/warning/);

  const row3 = page.getByTestId('deadline-row-SOL-2026-0103');
  await expect(row3).toContainText('02/12/2026');
  await expect(page.getByTestId('deadline-chip-SOL-2026-0103')).toHaveText('6 días');
  await expect(page.getByTestId('deadline-chip-SOL-2026-0103')).toHaveClass(/safe/);

  // Enlace Ver todas con ent_d y ent_h en Este mes, y sin ent_d/ent_h en Todo
  const verTodas = page.getByTestId('deadlines-ver-todas');
  await expect(verTodas).toHaveAttribute(
    'href',
    '/admin/solicitudes?dias=1-7&ent_d=2026-11-01&ent_h=2026-11-30',
  );

  await page.getByLabel('Periodo').selectOption('todo');
  await expect(verTodas).toHaveAttribute('href', '/admin/solicitudes?dias=1-7');

  // Navegación con Enter en el enlace del folio
  const folioLink = row3.getByRole('link', { name: 'SOL-2026-0103' });
  await folioLink.focus();
  await folioLink.press('Enter');
  const pushedAfterEnter = await readPushedUrls(page);
  expect(pushedAfterEnter).toContain('/admin/solicitudes/SOL-2026-0103');

  await page.goto('/admin/dashboard');

  // Clic en la fila navega a /admin/solicitudes/:folio codificando caracteres especiales
  await page.getByTestId('deadline-row-00045-A/2026').locator('.dashboard-cell-asunto').click();
  const pushedAfterClick = await readPushedUrls(page);
  expect(pushedAfterClick).toContain('/admin/solicitudes/00045-A%2F2026');
});

test('7. cambio de periodo descarta respuestas lentas de un periodo anterior y RefreshCw actualiza indicadores', async ({
  page,
}) => {
  let releaseSlowMesAnterior!: () => void;
  const slowGate = new Promise<void>((resolve) => {
    releaseSlowMesAnterior = resolve;
  });

  let esteMesVersion: 'initial' | 'refreshed' = 'initial';
  let esteMesCalls = 0;

  await setupDashboardRoutes(page, {
    onDashboard: async (route, periodo) => {
      if (periodo === 'este-mes') {
        esteMesCalls += 1;
        const isInitial = esteMesVersion === 'initial';
        return route.fulfill({
          json: buildFullDashboardMock({
            periodo: 'este-mes',
            actualizadoEnUtc: isInitial ? '2026-11-26T18:35:00Z' : '2026-11-26T19:10:00Z',
            indicadores: {
              total: isInitial ? 40 : 52,
              enProceso: isInitial ? 14 : 20,
              entregadas: isInitial ? 20 : 25,
              vencidas: isInitial ? 6 : 7,
              proximasAVencer: 4,
              variacionTotalPorcentaje: isInitial ? 12.5 : -5,
            },
            porArea: [
              {
                area: 'Dirección General de Desarrollo Urbano y Obras Públicas',
                cantidad: isInitial ? 40 : 52,
              },
            ],
          }),
        });
      }

      if (periodo === 'mes-anterior') {
        await slowGate;
        // Puede haber sido abortado por el navegador; ignorar error si ya se cerró
        try {
          await route.fulfill({
            json: buildFullDashboardMock({
              periodo: 'mes-anterior',
              fechaInicio: '2026-10-01',
              fechaFin: '2026-10-31',
              indicadores: {
                total: 99,
                enProceso: 33,
                entregadas: 33,
                vencidas: 33,
                proximasAVencer: 4,
                variacionTotalPorcentaje: 0,
              },
              porArea: [{ area: 'Área Antigua Mes Anterior', cantidad: 99 }],
            }),
          });
        } catch {
          // Solicitud abortada correctamente por AbortSignal
        }
        return;
      }

      if (periodo === 'este-anio') {
        return route.fulfill({
          json: buildFullDashboardMock({
            periodo: 'este-anio',
            fechaInicio: '2026-01-01',
            fechaFin: '2026-11-26',
            indicadores: {
              total: 120,
              enProceso: 40,
              entregadas: 60,
              vencidas: 20,
              proximasAVencer: 4,
              variacionTotalPorcentaje: null,
            },
            porArea: [{ area: 'Secretaría de Finanzas y Tesorería', cantidad: 120 }],
          }),
        });
      }

      return route.fulfill({ json: buildFullDashboardMock({ periodo: periodo ?? 'este-mes' }) });
    },
  });

  await page.goto('/admin/dashboard');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('40');

  // Actualización manual con botón RefreshCw (32x32px)
  const refreshBtn = page.getByRole('button', { name: 'Actualizar indicadores' });
  const refreshBox = await refreshBtn.boundingBox();
  expect(Math.round(refreshBox!.width)).toBe(32);
  expect(Math.round(refreshBox!.height)).toBe(32);

  const callsBeforeRefresh = esteMesCalls;
  esteMesVersion = 'refreshed';
  await refreshBtn.click();
  await expect(page.getByTestId('kpi-value-total')).toHaveText('52');
  expect(esteMesCalls).toBeGreaterThan(callsBeforeRefresh);
  await expect(page.getByTestId('kpi-secondary-total')).toHaveText('-5% vs. periodo anterior');
  await expect(page.getByTestId('dashboard-updated-status')).toHaveText('Actualizado a las 13:10');

  // Cambio rápido: Este mes -> Mes anterior (lento) -> Este año (rápido)
  const periodSelect = page.getByLabel('Periodo');
  await periodSelect.selectOption('mes-anterior');
  await expect(periodSelect).toHaveValue('mes-anterior');

  await periodSelect.selectOption('este-anio');
  await expect(periodSelect).toHaveValue('este-anio');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('120');
  // Variación null con total > 0 omite línea secundaria del Total
  await expect(page.getByTestId('kpi-secondary-total')).toHaveCount(0);

  // Liberamos la respuesta lenta de Mes anterior y comprobamos que NO sobrescribe Este año
  releaseSlowMesAnterior();
  await page.waitForTimeout(100);
  await expect(periodSelect).toHaveValue('este-anio');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('120');
  await expect(page.getByText('Área Antigua Mes Anterior')).toHaveCount(0);
});

test('8. estado vacío (total = 0): KPIs en 0 con Sin datos, Donut sin anillo ficticio y empty states en todos los widgets', async ({
  page,
}) => {
  await setupDashboardRoutes(page, {
    onDashboard: (route, periodo) =>
      route.fulfill({ json: buildEmptyDashboardMock(periodo ?? 'este-mes') }),
  });

  await page.goto('/admin/dashboard');

  for (const id of [
    'total',
    'en-proceso',
    'entregadas',
    'vencidas',
    'proximas-a-vencer',
  ] as const) {
    await expect(page.getByTestId(`kpi-value-${id}`)).toHaveText('0');
    await expect(page.getByTestId(`kpi-secondary-${id}`)).toHaveText('Sin datos');
  }

  // Donut sin anillo SVG ficticio
  await expect(page.getByTestId('donut-svg')).toHaveCount(0);
  await expect(page.getByTestId('donut-empty-state')).toContainText(
    'Sin solicitudes en este periodo',
  );
  await expect(page.getByTestId('donut-empty-state')).toContainText('Sin datos');

  // Próximos vencimientos vacío
  await expect(page.getByTestId('deadlines-empty-state')).toContainText(
    'Sin próximos vencimientos en este periodo',
  );

  // Barras por área vacías
  await expect(page.getByTestId('area-bar-0')).toHaveCount(0);
  await expect(page.getByTestId('area-empty-state')).toContainText(
    'Sin solicitudes registradas por área en este periodo',
  );

  // Actividad reciente vacía
  await expect(page.getByTestId('activity-empty-state')).toContainText('Sin actividad reciente');
});

test('9. error total (503), fallo al refrescar sin dejar cifras obsoletas y error parcial/inconsistente por widget con recuperación', async ({
  page,
}) => {
  let mode: 'ok' | 'fail-503' | 'partial-error' | 'invalid-kpi-only' = 'fail-503';

  await setupDashboardRoutes(page, {
    onDashboard: (route, periodo) => {
      if (mode === 'fail-503') {
        return route.fulfill({ status: 503, json: { title: 'Servicio no disponible' } });
      }
      if (mode === 'partial-error') {
        // Indicadores y vencimientos válidos; porArea inconsistente (suma 10 != 40) y actividadReciente no disponible
        return route.fulfill({
          json: buildFullDashboardMock({
            periodo: periodo ?? 'ultimos-3-meses',
            disponibilidad: {
              indicadores: true,
              porArea: true,
              proximosVencimientos: true,
              actividadReciente: false,
            },
            porArea: [{ area: 'Contraloría Municipal', cantidad: 10 }],
            actividadReciente: null,
          }),
        });
      }
      if (mode === 'invalid-kpi-only') {
        // Indicadores inconsistente (total 99 != 14+20+6), mientras porArea, vencimientos y actividad siguen válidos
        return route.fulfill({
          json: buildFullDashboardMock({
            periodo: periodo ?? 'ultimos-3-meses',
            indicadores: {
              total: 99,
              enProceso: 14,
              entregadas: 20,
              vencidas: 6,
              proximasAVencer: 4,
              variacionTotalPorcentaje: 5,
            },
          }),
        });
      }
      return route.fulfill({ json: buildFullDashboardMock({ periodo: periodo ?? 'este-mes' }) });
    },
  });

  // 9a. Error total inicial (503): muestra —, Información no disponible, jamás ceros
  await page.goto('/admin/dashboard');
  await expect(page.getByTestId('dashboard-updated-status')).toHaveText(
    'Información no disponible',
  );
  for (const id of [
    'total',
    'en-proceso',
    'entregadas',
    'vencidas',
    'proximas-a-vencer',
  ] as const) {
    await expect(page.getByTestId(`kpi-value-${id}`)).toHaveText('—');
    await expect(page.getByTestId(`kpi-secondary-${id}`)).toHaveText('Información no disponible');
  }
  await expect(page.getByTestId('widget-status-donut')).toContainText('Información no disponible');
  await expect(page.getByTestId('widget-upcoming-deadlines')).toContainText(
    'Información no disponible',
  );
  await expect(page.getByTestId('widget-area-chart')).toContainText('Información no disponible');
  await expect(page.getByTestId('widget-recent-activity')).toContainText(
    'Información no disponible',
  );

  // Conserva periodo seleccionado y se recupera al pulsar Reintentar
  await page.getByLabel('Periodo').selectOption('ultimos-3-meses');
  await expect(page.getByLabel('Periodo')).toHaveValue('ultimos-3-meses');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('—');

  mode = 'ok';
  await page.getByTestId('widget-status-donut').getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByLabel('Periodo')).toHaveValue('ultimos-3-meses');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('40');

  // 9b. Fallo al refrescar con RefreshCw: NO conserva cifras previas como vigentes
  mode = 'fail-503';
  await page.getByRole('button', { name: 'Actualizar indicadores' }).click();
  await expect(page.getByTestId('kpi-value-total')).toHaveText('—');
  await expect(page.getByTestId('donut-center-total')).toHaveCount(0);
  await expect(page.getByTestId('dashboard-updated-status')).toHaveText(
    'Información no disponible',
  );

  // 9c. Error parcial / inconsistencia aislada por widget
  mode = 'partial-error';
  await page.getByTestId('kpi-retry-btn').click();

  // KPIs, Donut y Próximos vencimientos funcionan normalmente
  await expect(page.getByTestId('kpi-value-total')).toHaveText('40');
  await expect(page.getByTestId('donut-center-total')).toHaveText('40');
  await expect(page.getByTestId('deadline-row-00045-A/2026')).toBeVisible();

  // Barras por área (inconsistente con total) y Actividad reciente (disponibilidad=false) quedan aisladas en error
  await expect(page.getByTestId('widget-area-chart')).toContainText('Información no disponible');
  await expect(
    page.getByTestId('widget-area-chart').getByRole('button', { name: 'Reintentar' }),
  ).toBeVisible();
  await expect(page.getByTestId('widget-recent-activity')).toContainText(
    'Información no disponible',
  );
  await expect(
    page.getByTestId('widget-recent-activity').getByRole('button', { name: 'Reintentar' }),
  ).toBeVisible();

  // 9d. Indicadores inconsistentes aíslan KPIs y Donut sin tumbar los demás widgets válidos
  mode = 'invalid-kpi-only';
  await page.getByTestId('widget-area-chart').getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByTestId('kpi-value-total')).toHaveText('—');
  await expect(page.getByTestId('widget-status-donut')).toContainText('Información no disponible');
  await expect(page.getByTestId('area-bar-0')).toBeVisible();
  await expect(page.getByTestId('deadline-row-00045-A/2026')).toBeVisible();
  await expect(page.getByTestId('activity-list')).toBeVisible();
});

test('10. diseño responsive en 1440x900, 1024x768 (KPI 3+2 y widgets apilados) y 390x844 (KPI 2+2+1 y tarjetas en vencimientos)', async ({
  page,
}) => {
  await setupDashboardRoutes(page);

  // Desktop 1440x900
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/admin/dashboard');
  await expect(page.getByTestId('kpi-value-total')).toHaveText('40');

  const kpi1Desktop = await page.getByTestId('kpi-card-total').boundingBox();
  const kpi5Desktop = await page.getByTestId('kpi-card-proximas-a-vencer').boundingBox();
  expect(Math.round(kpi1Desktop!.y)).toBe(Math.round(kpi5Desktop!.y));
  expect(Math.round(kpi1Desktop!.height)).toBe(112);

  const donutDesktop = await page.getByTestId('widget-status-donut').boundingBox();
  const deadlinesDesktop = await page.getByTestId('widget-upcoming-deadlines').boundingBox();
  expect(Math.round(donutDesktop!.y)).toBe(Math.round(deadlinesDesktop!.y));

  await expect(page.getByTestId('deadline-row-00045-A/2026')).toHaveCSS('display', 'table-row');

  // Desktop compacto 1280x800: conserva 5 KPIs en fila y fila 2 en dos columnas
  await page.setViewportSize({ width: 1280, height: 800 });
  const kpi1At1280 = await page.getByTestId('kpi-card-total').boundingBox();
  const kpi5At1280 = await page.getByTestId('kpi-card-proximas-a-vencer').boundingBox();
  expect(Math.round(kpi1At1280!.y)).toBe(Math.round(kpi5At1280!.y));
  const donutAt1280 = await page.getByTestId('widget-status-donut').boundingBox();
  const deadlinesAt1280 = await page.getByTestId('widget-upcoming-deadlines').boundingBox();
  expect(Math.round(donutAt1280!.y)).toBe(Math.round(deadlinesAt1280!.y));

  // Tablet 1024x768: KPIs en distribución 3+2 y widgets apilados a ancho completo
  await page.setViewportSize({ width: 1024, height: 768 });
  const kpi1Tablet = await page.getByTestId('kpi-card-total').boundingBox();
  const kpi3Tablet = await page.getByTestId('kpi-card-entregadas').boundingBox();
  const kpi4Tablet = await page.getByTestId('kpi-card-vencidas').boundingBox();
  const kpi5Tablet = await page.getByTestId('kpi-card-proximas-a-vencer').boundingBox();

  expect(Math.round(kpi1Tablet!.y)).toBe(Math.round(kpi3Tablet!.y));
  expect(kpi4Tablet!.y).toBeGreaterThan(kpi1Tablet!.y + 80);
  expect(Math.round(kpi4Tablet!.y)).toBe(Math.round(kpi5Tablet!.y));

  const donutTablet = await page.getByTestId('widget-status-donut').boundingBox();
  const deadlinesTablet = await page.getByTestId('widget-upcoming-deadlines').boundingBox();
  expect(deadlinesTablet!.y).toBeGreaterThan(donutTablet!.y + 200);

  // Frontera exacta <= 768px (768x900): KPIs 2 cols + 5ta completa y vencimientos en tarjetas
  await page.setViewportSize({ width: 768, height: 900 });
  const kpi1At768 = await page.getByTestId('kpi-card-total').boundingBox();
  const kpi2At768 = await page.getByTestId('kpi-card-en-proceso').boundingBox();
  const kpi5At768 = await page.getByTestId('kpi-card-proximas-a-vencer').boundingBox();
  expect(Math.round(kpi1At768!.y)).toBe(Math.round(kpi2At768!.y));
  expect(kpi5At768!.width).toBeGreaterThan(kpi1At768!.width * 1.8);
  await expect(page.getByTestId('deadline-row-00045-A/2026')).toHaveCSS('display', 'grid');

  // Móvil 390x844 (<= 768px): KPIs 2 cols + 5ta completa y vencimientos como tarjetas sin desborde horizontal
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() =>
      page.evaluate(() => {
        const g = globalThis as unknown as BrowserSpyGlobal;
        return g.document.documentElement.scrollWidth <= g.innerWidth;
      }),
    )
    .toBe(true);

  const kpi1Mobile = await page.getByTestId('kpi-card-total').boundingBox();
  const kpi2Mobile = await page.getByTestId('kpi-card-en-proceso').boundingBox();
  const kpi5Mobile = await page.getByTestId('kpi-card-proximas-a-vencer').boundingBox();

  expect(Math.round(kpi1Mobile!.y)).toBe(Math.round(kpi2Mobile!.y));
  expect(kpi5Mobile!.width).toBeGreaterThan(kpi1Mobile!.width * 1.8);

  await expect(page.getByTestId('deadline-row-00045-A/2026')).toHaveCSS('display', 'grid');

  const areaScrollable = await page
    .getByTestId('area-chart-scroll')
    .evaluate((el) => {
      const box = el as unknown as { scrollWidth: number; clientWidth: number };
      return box.scrollWidth > box.clientWidth;
    });
  expect(areaScrollable).toBe(true);
});

test('11. no duplica Otras si ya viene del backend, valida contradicción en próximos vencimientos, soporta Enter en folio y menú móvil Solicitudes', async ({
  page,
}) => {
  await installPushStateSpy(page);

  await setupDashboardRoutes(page, {
    onDashboard: (route, periodo) =>
      route.fulfill({
        json: buildFullDashboardMock({
          periodo: periodo ?? 'este-mes',
          indicadores: {
            total: 40,
            enProceso: 14,
            entregadas: 20,
            vencidas: 6,
            proximasAVencer: 3,
            variacionTotalPorcentaje: 0,
          },
          // 7 áreas regulares + "Otras" ya enviada por el backend (suma = 10+8+6+5+4+3+2+2 = 40)
          porArea: [
            { area: 'Área 1', cantidad: 10 },
            { area: 'Área 2', cantidad: 8 },
            { area: 'Área 3', cantidad: 6 },
            { area: 'Área 4', cantidad: 5 },
            { area: 'Área 5', cantidad: 4 },
            { area: 'Área 6', cantidad: 3 },
            { area: 'Área 7', cantidad: 2 },
            { area: 'Otras', cantidad: 2 },
          ],
          // Contradicción intencional: proximasAVencer=3 pero proximosVencimientos=[]
          proximosVencimientos: [],
        }),
      }),
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/admin/dashboard');

  // Variación 0%
  await expect(page.getByTestId('kpi-secondary-total')).toHaveText('0% vs. periodo anterior');

  // "Otras" aparece una sola vez al final con 2 (Área 7) + 2 (Otras) = 4
  await expect(page.getByTestId('area-bar-6')).toHaveAttribute('data-area', 'Otras');
  await expect(page.getByTestId('area-bar-6')).toHaveAttribute('data-count', '4');
  await expect(page.getByTestId('area-bar-7')).toHaveCount(0);

  // Próximos vencimientos detecta contradicción con proximasAVencer=3 y muestra estado no disponible
  await expect(page.getByTestId('widget-upcoming-deadlines')).toContainText(
    'Información no disponible',
  );

  // Drawer móvil: abrir menú lateral y pulsar Solicitudes navega a /admin/solicitudes
  await page.getByRole('button', { name: 'Abrir menú lateral' }).click();
  await page.getByRole('button', { name: 'Solicitudes' }).click();

  const pushedUrls = await readPushedUrls(page);
  expect(pushedUrls).toContain('/admin/solicitudes');
});

test('12. estado loading explícito con skeletons, prefers-reduced-motion: reduce, feedback de reintento parcial y actividad independiente de total=0', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.setViewportSize({ width: 768, height: 900 });

  let releaseInitial!: () => void;
  const initialGate = new Promise<void>((resolve) => {
    releaseInitial = resolve;
  });

  let releaseRetry!: () => void;
  const retryGate = new Promise<void>((resolve) => {
    releaseRetry = resolve;
  });

  let step: 'initial-partial' | 'retry-recovered' = 'initial-partial';

  await setupDashboardRoutes(page, {
    onDashboard: async (route, periodo) => {
      if (step === 'initial-partial') {
        await initialGate;
        // total=0 pero con actividad reciente válida y porArea no disponible (error parcial)
        try {
          await route.fulfill({
            json: {
              ...buildEmptyDashboardMock(periodo ?? 'este-mes'),
              disponibilidad: {
                indicadores: true,
                porArea: false,
                proximosVencimientos: true,
                actividadReciente: true,
              },
              porArea: null,
              actividadReciente: [
                {
                  tipo: 'NuevaSolicitud',
                  descripcion: 'Actividad global verificable',
                  folio: 'SOL-GLOBAL-01',
                  ocurridoEnUtc: '2026-11-26T18:20:00Z',
                },
              ],
            },
          });
        } catch {
          // Solicitud previa cancelada por AbortSignal
        }
        return;
      }

      await retryGate;
      return route.fulfill({
        json: {
          ...buildEmptyDashboardMock(periodo ?? 'este-mes'),
          actividadReciente: [
            {
              tipo: 'NuevaSolicitud',
              descripcion: 'Actividad global verificable',
              folio: 'SOL-GLOBAL-01',
              ocurridoEnUtc: '2026-11-26T18:20:00Z',
            },
          ],
        },
      });
    },
  });

  await page.goto('/admin/dashboard');

  // Mientras la petición inicial está en vuelo: skeletons visibles, periodo conservado y reduced-motion activo
  await expect(page.getByTestId('dashboard-updated-status')).toHaveText('Actualizando…');
  await expect(page.getByLabel('Periodo')).toHaveValue('este-mes');
  await expect(page.getByTestId('kpi-card-total')).toHaveAttribute('aria-busy', 'true');

  const skeletonBar = page.locator('.dashboard-skeleton-bar').first();
  await expect(skeletonBar).toBeVisible();
  const skeletonBarBox = await skeletonBar.boundingBox();
  expect(skeletonBarBox!.height).toBeGreaterThan(60);
  await expect(skeletonBar).toHaveCSS('animation-name', 'none');
  await expect(page.locator('.dashboard-spin')).toHaveCSS('animation-name', 'none');

  // Resolvemos la carga inicial: total=0 convive con actividadReciente válida y porArea queda en error parcial
  releaseInitial();
  await expect(page.getByTestId('kpi-value-total')).toHaveText('0');
  await expect(page.getByTestId('widget-recent-activity')).toContainText('SOL-GLOBAL-01');
  await expect(page.getByTestId('widget-area-chart')).toContainText('Información no disponible');

  // Al pulsar Reintentar en el error parcial, el widget en error pasa de inmediato a skeleton (loading)
  step = 'retry-recovered';
  await page.getByTestId('widget-area-chart').getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByTestId('widget-area-chart').locator('.dashboard-area-loading')).toBeVisible();
  await expect(page.getByTestId('kpi-value-total')).toHaveText('0');

  releaseRetry();
  await expect(page.getByTestId('area-empty-state')).toContainText(
    'Sin solicitudes registradas por área en este periodo',
  );
});
