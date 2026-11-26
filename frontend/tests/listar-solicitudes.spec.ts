import { expect, test, type Page, type Route } from '@playwright/test';
import { randomUUID } from 'node:crypto';

interface MockListadoItem {
  id: string;
  folio: string;
  asunto: string;
  areaReceptora: string;
  medioEntrega: string;
  estatusEfectivo: string;
  fechaEntrega: string;
  fechaVencimiento: string;
  diasRestantes: number;
  version: string;
}

const MOCK_CATALOGOS = {
  areasReceptoras: [
    { id: 'direccion-obras-publicas', nombre: 'Dirección de Obras Públicas' },
    { id: 'comision-estatal-agua', nombre: 'Comisión Estatal del Agua' },
    { id: 'instituto-infraestructura', nombre: 'Instituto de Infraestructura Educativa' },
  ],
  mediosEntrega: [
    { codigo: 'Digital', nombre: 'Digital' },
    { codigo: 'Impreso', nombre: 'Impreso' },
    { codigo: 'PortalWeb', nombre: 'Portal web' },
    { codigo: 'CorreoElectronico', nombre: 'Correo electrónico' },
  ],
  estatusEntrega: [
    { codigo: 'EnProceso', nombre: 'En proceso' },
    { codigo: 'Entregada', nombre: 'Entregada' },
    { codigo: 'Vencida', nombre: 'Vencida' },
  ],
  tiposSolicitud: [
    { codigo: 'Informacion', nombre: 'Información' },
    { codigo: 'Aclaracion', nombre: 'Aclaración' },
  ],
  tiposEvidencia: [
    { codigo: 'Carpeta', nombre: 'Carpeta' },
    { codigo: 'Link', nombre: 'Link' },
  ],
  tiposAcuse: [
    { codigo: 'Sello', nombre: 'Sello' },
    { codigo: 'Folio', nombre: 'Folio' },
    { codigo: 'NoAplica', nombre: 'No aplica' },
  ],
};

function buildSampleItems(): MockListadoItem[] {
  return [
    {
      id: 'sol-001',
      folio: 'SOL-2025-001',
      asunto: 'Rehabilitación de pavimento hidráulico en avenida principal oriente',
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Digital',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: '2025-06-10',
      fechaVencimiento: '2025-06-30',
      diasRestantes: 13,
      version: 'ver-001-a',
    },
    {
      id: 'sol-002',
      folio: '00045-A/2026',
      asunto: 'Construcción de guarniciones y banquetas en colonia centro',
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Impreso',
      estatusEfectivo: 'Entregada',
      fechaEntrega: '2025-06-10',
      fechaVencimiento: '2025-06-25',
      diasRestantes: 8,
      version: '"ver-002-quoted"',
    },
    {
      id: 'sol-003',
      folio: 'SOL_2025_003',
      asunto: 'Ampliación de red de drenaje sanitario etapa dos',
      areaReceptora: 'Comisión Estatal del Agua',
      medioEntrega: 'PortalWeb',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: '2025-06-12',
      fechaVencimiento: '2025-06-28',
      diasRestantes: 3,
      version: 'ver-003-c',
    },
    {
      id: 'sol-004',
      folio: 'SOL-2025-004',
      asunto: 'Mejoramiento de alumbrado público y subestación eléctrica',
      areaReceptora: 'Correo Institucional Obras',
      medioEntrega: 'CorreoElectronico',
      estatusEfectivo: 'Vencida',
      fechaEntrega: '2025-06-01',
      fechaVencimiento: '2025-06-09',
      diasRestantes: 0,
      version: 'ver-004-d',
    },
  ];
}

async function setupV04Routes(
  page: Page,
  options: {
    roles?: string[] | null;
    initialItems?: MockListadoItem[];
    onGetSolicitudes?: (
      route: Route,
      url: URL,
      count: number,
      items: MockListadoItem[],
    ) => Promise<void> | void;
    onDeleteSolicitud?: (
      route: Route,
      id: string,
      headers: Record<string, string>,
      count: number,
      items: MockListadoItem[],
    ) => Promise<void> | void;
    onGetEdicion?: (
      route: Route,
      folio: string | null,
      count: number,
      items: MockListadoItem[],
    ) => Promise<void> | void;
    onCatalogos?: (route: Route, count: number) => Promise<void> | void;
  } = {},
) {
  const {
    roles = ['Administrador'],
    initialItems = buildSampleItems(),
    onGetSolicitudes,
    onDeleteSolicitud,
    onGetEdicion,
    onCatalogos,
  } = options;

  const store = initialItems.map((item) => ({ ...item }));
  let currentRoles: string[] | null = roles;
  let csrfToken = `csrf-${randomUUID()}`;
  let getListCount = 0;
  let deleteCount = 0;
  let getEdicionCount = 0;
  let catalogosCount = 0;
  const seenListUrls: URL[] = [];

  await page.route('**/api/**', async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname;

    if (!path.startsWith('/api/')) {
      return route.continue();
    }

    if (path === '/api/auth/me' && req.method() === 'GET') {
      if (!currentRoles) {
        return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
      }
      return route.fulfill({
        json: {
          userId: 'admin-1',
          userName: 'admin@obras.gob.mx',
          roles: currentRoles,
        },
      });
    }

    if (path === '/api/auth/csrf' && req.method() === 'GET') {
      csrfToken = `csrf-${randomUUID()}`;
      return route.fulfill({ json: { requestToken: csrfToken } });
    }

    if (path === '/api/auth/logout' && req.method() === 'POST') {
      currentRoles = null;
      return route.fulfill({ status: 204 });
    }

    if (path === '/api/solicitudes/catalogos' && req.method() === 'GET') {
      catalogosCount += 1;
      if (onCatalogos) return onCatalogos(route, catalogosCount);
      return route.fulfill({ json: MOCK_CATALOGOS });
    }

    if (path === '/api/solicitudes/edicion' && req.method() === 'GET') {
      getEdicionCount += 1;
      const folioParam = url.searchParams.get('folio');
      if (onGetEdicion) {
        return onGetEdicion(route, folioParam, getEdicionCount, store);
      }
      const found = store.find((item) => item.folio === folioParam);
      if (!found) {
        return route.fulfill({ status: 404, json: { title: 'Not Found' } });
      }
      const normalizedEtag = found.version.startsWith('"')
        ? found.version
        : `"${found.version}"`;
      return route.fulfill({
        status: 200,
        headers: { ETag: normalizedEtag },
        json: {
          id: found.id,
          folio: found.folio,
          fechaEntrega: found.fechaEntrega,
          horaEntrega: '10:30',
          areaReceptoraId: 'direccion-obras-publicas',
          asunto: found.asunto,
          medioEntrega: found.medioEntrega,
          estatusEntrega: found.estatusEfectivo === 'Entregada' ? 'Entregada' : 'EnProceso',
          tipoSolicitud: 'Informacion',
          tipoEvidencia: 'Carpeta',
          tieneAcuse: found.estatusEfectivo === 'Entregada',
          tipoAcuse: found.estatusEfectivo === 'Entregada' ? 'Sello' : 'NoAplica',
          fechaVencimiento: found.fechaVencimiento,
          diasRestantes: found.diasRestantes,
          estatusEfectivo: found.estatusEfectivo,
        },
      });
    }

    if (path === '/api/solicitudes' && req.method() === 'GET') {
      getListCount += 1;
      seenListUrls.push(url);
      if (onGetSolicitudes) {
        return onGetSolicitudes(route, url, getListCount, store);
      }

      const pageNum = Math.max(1, Number(url.searchParams.get('page') ?? '1'));
      const pageSize = Number(url.searchParams.get('size') ?? '10');
      const q = (url.searchParams.get('q') ?? '').trim().toLowerCase();

      let filtered = [...store];
      if (q) {
        filtered = filtered.filter((item) => {
          const entregaFormatted = item.fechaEntrega.split('-').reverse().join('/');
          const vencFormatted = item.fechaVencimiento.split('-').reverse().join('/');
          return (
            item.folio.toLowerCase().includes(q) ||
            entregaFormatted.includes(q) ||
            vencFormatted.includes(q)
          );
        });
      }

      const dias = url.searchParams.get('dias');
      if (dias === '0') {
        filtered = filtered.filter(
          (i) => i.estatusEfectivo !== 'Entregada' && i.diasRestantes <= 0,
        );
      } else if (dias === '1-3') {
        filtered = filtered.filter(
          (i) =>
            i.estatusEfectivo !== 'Entregada' && i.diasRestantes >= 1 && i.diasRestantes <= 3,
        );
      } else if (dias === '4-7') {
        filtered = filtered.filter(
          (i) =>
            i.estatusEfectivo !== 'Entregada' && i.diasRestantes >= 4 && i.diasRestantes <= 7,
        );
      } else if (dias === '1-7') {
        filtered = filtered.filter(
          (i) =>
            i.estatusEfectivo !== 'Entregada' && i.diasRestantes >= 1 && i.diasRestantes <= 7,
        );
      } else if (dias === '8+') {
        filtered = filtered.filter(
          (i) => i.estatusEfectivo !== 'Entregada' && i.diasRestantes >= 8,
        );
      } else if (dias === 'custom') {
        const minStr = url.searchParams.get('dias_min');
        const maxStr = url.searchParams.get('dias_max');
        const min = minStr !== null ? Number(minStr) : 0;
        const max = maxStr !== null ? Number(maxStr) : 999;
        filtered = filtered.filter(
          (i) =>
            i.estatusEfectivo !== 'Entregada' &&
            i.diasRestantes >= min &&
            i.diasRestantes <= max,
        );
      }

      const entD = url.searchParams.get('ent_d');
      const entH = url.searchParams.get('ent_h');
      if (entD) filtered = filtered.filter((i) => i.fechaEntrega >= entD);
      if (entH) filtered = filtered.filter((i) => i.fechaEntrega <= entH);

      const venD = url.searchParams.get('ven_d');
      const venH = url.searchParams.get('ven_h');
      if (venD) filtered = filtered.filter((i) => i.fechaVencimiento >= venD);
      if (venH) filtered = filtered.filter((i) => i.fechaVencimiento <= venH);

      const sort = url.searchParams.get('sort') ?? 'vencimiento';
      const dir = url.searchParams.get('dir') === 'desc' ? -1 : 1;
      filtered.sort((a, b) => {
        if (sort === 'folio') return a.folio.localeCompare(b.folio) * dir;
        if (sort === 'entrega') return a.fechaEntrega.localeCompare(b.fechaEntrega) * dir;
        if (sort === 'dias') return (a.diasRestantes - b.diasRestantes) * dir;
        return a.fechaVencimiento.localeCompare(b.fechaVencimiento) * dir;
      });

      const totalItems = filtered.length;
      const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
      const start = (pageNum - 1) * pageSize;
      const items = filtered.slice(start, start + pageSize);

      return route.fulfill({
        status: 200,
        json: {
          items,
          page: pageNum,
          pageSize,
          totalItems,
          totalPages,
        },
      });
    }

    if (path.startsWith('/api/solicitudes/') && req.method() === 'DELETE') {
      const headerCsrf = req.headers()['x-csrf-token'];
      if (!headerCsrf || headerCsrf !== csrfToken) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }
      deleteCount += 1;
      const id = decodeURIComponent(path.replace('/api/solicitudes/', ''));
      if (onDeleteSolicitud) {
        return onDeleteSolicitud(route, id, req.headers(), deleteCount, store);
      }

      const idx = store.findIndex((item) => item.id === id);
      if (idx === -1) {
        return route.fulfill({ status: 404, json: { title: 'Not Found' } });
      }
      const expectedEtag = store[idx].version.startsWith('"')
        ? store[idx].version
        : `"${store[idx].version}"`;
      if (req.headers()['if-match'] !== expectedEtag) {
        return route.fulfill({ status: 412, json: { title: 'Precondition Failed' } });
      }
      store.splice(idx, 1);
      return route.fulfill({ status: 204 });
    }

    return route.abort();
  });

  return {
    store,
    seenListUrls,
    setRoles: (next: string[] | null) => {
      currentRoles = next;
    },
    getDeleteCount: () => deleteCount,
    getListCount: () => getListCount,
    getEdicionCount: () => getEdicionCount,
  };
}

test('autorización en /admin/solicitudes y /admin/solicitudes/:folio: anónimo redirige a login y Consulta recibe Acceso denegado sin llamar API', async ({
  page,
}) => {
  const anonCtx = await setupV04Routes(page, { roles: null });
  await page.goto('/admin/solicitudes');
  await expect(page).toHaveURL(/\/admin\/login$/);
  expect(anonCtx.getListCount()).toBe(0);

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}`);
  await expect(page).toHaveURL(/\/admin\/login$/);
  expect(anonCtx.getEdicionCount()).toBe(0);

  await page.unrouteAll();
  const consultaCtx = await setupV04Routes(page, { roles: ['Consulta'] });
  await page.goto('/admin/solicitudes');
  await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Ir a mi panel' })).toBeVisible();
  expect(consultaCtx.getListCount()).toBe(0);

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}`);
  await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
  expect(consultaCtx.getEdicionCount()).toBe(0);
});

test('carga skeleton de 6 filas, prefers-reduced-motion y renderizado completo de columnas, medios legibles, estatus y guion en Entregada', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });

  let holdList = true;
  let releaseList!: () => void;
  const listGate = new Promise<void>((resolve) => {
    releaseList = resolve;
  });

  await setupV04Routes(page, {
    onGetSolicitudes: async (route, _url, _count, store) => {
      if (holdList) {
        await listGate;
      }
      return route.fulfill({
        status: 200,
        json: {
          items: store,
          page: 1,
          pageSize: 10,
          totalItems: store.length,
          totalPages: 1,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes');

  await expect(page.getByRole('heading', { name: 'Solicitudes', level: 1 })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText(
    'Solicitudes›Listado',
  );
  await expect(page.getByTestId('solicitud-skeleton-row')).toHaveCount(6);
  await expect(page.locator('.v04-skeleton-bar').first()).toHaveCSS(
    'animation-name',
    'none',
  );

  holdList = false;
  releaseList();

  await expect(page.getByTestId('solicitud-skeleton-row')).toHaveCount(0);
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();

  // Verificar etiquetas legibles de medios
  await expect(page.getByRole('cell', { name: 'Digital' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Impreso' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Portal web' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Correo electrónico' })).toBeVisible();

  // Verificar estatus y días restantes (incluyendo guion — en Entregada)
  const rowEntregada = page.locator('tbody tr', { hasText: '00045-A/2026' });
  await expect(rowEntregada.locator('.v04-estatus-badge')).toHaveText('Entregada');
  await expect(rowEntregada.locator('.v04-col-dias')).toHaveText('—');
  await expect(rowEntregada.locator('.v04-col-entrega')).toHaveText('10/06/2025');
  await expect(rowEntregada.locator('.v04-col-vencimiento')).toHaveText('25/06/2025');

  const rowVerde = page.locator('tbody tr', { hasText: 'SOL-2025-001' });
  await expect(rowVerde.locator('.v04-estatus-badge')).toHaveText('En proceso');
  await expect(rowVerde.locator('.v04-dias-chip.is-green')).toHaveText('13 días');

  const rowNaranja = page.locator('tbody tr', { hasText: 'SOL_2025_003' });
  await expect(rowNaranja.locator('.v04-dias-chip.is-orange')).toHaveText('3 días');

  const rowRoja = page.locator('tbody tr', { hasText: 'SOL-2025-004' });
  await expect(rowRoja.locator('.v04-estatus-badge')).toHaveText('Vencida');
  await expect(rowRoja.locator('.v04-dias-chip.is-red')).toHaveText('0 días');
});

test('búsqueda por folio con /, _, ceros iniciales, fecha dd/mm/aaaa, debounce 300ms, Enter inmediato y limpieza con X', async ({
  page,
}) => {
  const ctx = await setupV04Routes(page);
  await page.goto('/admin/solicitudes');
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();
  const initialCount = ctx.getListCount();

  const searchBox = page.getByPlaceholder('Buscar por folio o fecha (dd/mm/aaaa)');

  // Debounce 300ms: escribir folio con ceros iniciales y barra /
  await searchBox.fill('00045-A/2026');
  // Antes de 300ms no se envía petición nueva
  expect(ctx.getListCount()).toBe(initialCount);

  await expect(page).toHaveURL(/q=00045-A%2F2026/);
  await expect(page.getByText('Mostrando 1 a 1 de 1 solicitudes')).toBeVisible();
  await expect(page.getByRole('cell', { name: '00045-A/2026', exact: true })).toBeVisible();

  // Limpiar con botón X
  await page.getByRole('button', { name: 'Limpiar búsqueda' }).click();
  await expect(searchBox).toHaveValue('');
  await expect(page).not.toHaveURL(/q=/);
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();

  // Enter inmediato con guion bajo _ cancelando espera de debounce
  const countBeforeEnter = ctx.getListCount();
  await searchBox.fill('SOL_2025_003');
  await searchBox.press('Enter');
  await expect(page).toHaveURL(/q=SOL_2025_003/);
  await expect(page.getByText('Mostrando 1 a 1 de 1 solicitudes')).toBeVisible();
  expect(ctx.getListCount()).toBe(countBeforeEnter + 1);

  // Búsqueda por fecha válida dd/mm/aaaa
  await searchBox.fill('12/06/2025');
  await searchBox.press('Enter');
  await expect(page.getByRole('cell', { name: 'SOL_2025_003', exact: true })).toBeVisible();
  await expect(page.getByText('Mostrando 1 a 1 de 1 solicitudes')).toBeVisible();
});

test('fecha calendario imposible (31/02/2026) y respuesta 400 por URL muestran estado amigable sin coincidencias y jamás B-04', async ({
  page,
}) => {
  await setupV04Routes(page, {
    onGetSolicitudes: (route, url, _count, store) => {
      if (url.searchParams.get('q') === 'BAD-URL-400') {
        return route.fulfill({
          status: 400,
          json: { title: 'Criterio inválido' },
        });
      }
      return route.fulfill({
        status: 200,
        json: {
          items: store,
          page: 1,
          pageSize: 10,
          totalItems: store.length,
          totalPages: 1,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes');
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();

  const searchBox = page.getByPlaceholder('Buscar por folio o fecha (dd/mm/aaaa)');
  await searchBox.fill('31/02/2026');
  await searchBox.press('Enter');

  // No dispara banner técnico B-04 (role="alert")
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByTestId('friendly-criteria-hint')).toContainText('31/02/2026');
  await expect(page.getByTestId('empty-no-matches')).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'No se encontraron solicitudes' }),
  ).toBeVisible();
  await expect(
    page.getByText('Ninguna solicitud coincide con los criterios aplicados.'),
  ).toBeVisible();
  await expect(searchBox).toHaveValue('31/02/2026');

  // Limpiar desde estado sin coincidencias recupera listado completo
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  await expect(searchBox).toHaveValue('');
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();

  // Caso URL directa donde el backend responde 400 -> tampoco muestra B-04
  await page.goto('/admin/solicitudes?q=BAD-URL-400');
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByTestId('friendly-criteria-hint')).toBeVisible();
  await expect(page.getByTestId('empty-no-matches')).toBeVisible();
});

test('popover de filtros: validaciones, Escape, clic fuera, chips activos, contador badge, retiro individual y contrato dias=1-7', async ({
  page,
}) => {
  const ctx = await setupV04Routes(page);
  await page.goto('/admin/solicitudes');
  await expect(page.getByText('Mostrando 1 a 4 de 4 solicitudes')).toBeVisible();

  // Abrir popover y validar cierre con Escape y clic fuera
  await page.getByRole('button', { name: 'Filtros' }).click();
  const popover = page.getByRole('dialog', { name: 'Filtros de solicitudes' });
  await expect(popover).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(popover).toHaveCount(0);

  await page.getByRole('button', { name: 'Filtros' }).click();
  await expect(popover).toBeVisible();
  await page.getByRole('heading', { name: 'Solicitudes', level: 1 }).click();
  await expect(popover).toHaveCount(0);

  // Validaciones de fechas y rango personalizado antes de aplicar
  await page.getByRole('button', { name: 'Filtros' }).click();
  await page.getByLabel('Entrega desde').fill('31/02/2026');
  await page.getByRole('button', { name: 'Aplicar' }).click();
  await expect(popover).toContainText('Ingresa una fecha inicial válida (dd/mm/aaaa).');

  await page.getByLabel('Entrega desde').fill('20/06/2025');
  await page.getByLabel('Entrega hasta').fill('10/06/2025');
  await page.getByRole('button', { name: 'Aplicar' }).click();
  await expect(popover).toContainText(
    'La fecha final de entrega no puede ser anterior a la fecha inicial.',
  );

  await page.getByLabel('Entrega hasta').fill('30/06/2025');
  await page.getByLabel('Días restantes').selectOption('custom');
  await page.getByRole('button', { name: 'Aplicar' }).click();
  await expect(popover).toContainText('Ingresa al menos el mínimo o máximo de días (0 a 999).');

  await page.getByLabel('Mínimo').fill('1000');
  await page.getByRole('button', { name: 'Aplicar' }).click();
  await expect(popover).toContainText('El mínimo debe ser un número entero entre 0 y 999.');

  await page.getByLabel('Mínimo').fill('15');
  await page.getByLabel('Máximo').fill('5');
  await page.getByRole('button', { name: 'Aplicar' }).click();
  await expect(popover).toContainText('El mínimo de días no puede ser mayor al máximo.');

  // Aplicar filtros válidos: Entrega + Vencimiento + Personalizado (2..15)
  await page.getByLabel('Entrega desde').fill('01/06/2025');
  await page.getByLabel('Entrega hasta').fill('15/06/2025');
  await page.getByLabel('Vencimiento desde').fill('20/06/2025');
  await page.getByLabel('Vencimiento hasta').fill('30/06/2025');
  await page.getByLabel('Mínimo').fill('2');
  await page.getByLabel('Máximo').fill('15');
  await page.getByRole('button', { name: 'Aplicar' }).click();

  await expect(popover).toHaveCount(0);
  await expect(page.getByTestId('active-filters-badge')).toHaveText('3');
  await expect(page.getByTestId('filter-chip-entrega')).toContainText(
    'Entrega: 01/06/2025 – 15/06/2025',
  );
  await expect(page.getByTestId('filter-chip-vencimiento')).toContainText(
    'Vencimiento: 20/06/2025 – 30/06/2025',
  );
  await expect(page.getByTestId('filter-chip-dias')).toContainText(
    'Días restantes: 2–15 días',
  );
  await expect(page).toHaveURL(/ent_d=2025-06-01/);
  await expect(page).toHaveURL(/dias=custom&dias_min=2&dias_max=15/);

  // Retirar chip individualmente con X
  await page
    .getByRole('button', { name: 'Quitar filtro Días restantes: 2–15 días' })
    .click();
  await expect(page.getByTestId('active-filters-badge')).toHaveText('2');
  await expect(page).not.toHaveURL(/dias=/);
  await expect(page).not.toHaveURL(/dias_min=/);

  // Limpiar filtros desde la barra de chips
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  await expect(page.getByTestId('active-filters-badge')).toHaveCount(0);

  // Contrato compartido con Task 05: /admin/solicitudes?dias=1-7
  await page.goto('/admin/solicitudes?dias=1-7');
  await expect(page.getByTestId('filter-chip-dias')).toContainText('Días restantes: 1–7 días');
  await expect(page.getByTestId('active-filters-badge')).toHaveText('1');
  expect(ctx.seenListUrls.at(-1)?.searchParams.get('dias')).toBe('1-7');
  await expect(page.getByRole('cell', { name: 'SOL_2025_003', exact: true })).toBeVisible();
});

test('ordenamiento de columnas, paginación (10/25/50) y restablecimiento a página 1 conservando sort, dir y size', async ({
  page,
}) => {
  const twentyFiveItems: MockListadoItem[] = Array.from({ length: 25 }, (_, i) => {
    const num = String(i + 1).padStart(3, '0');
    return {
      id: `item-${num}`,
      folio: `SOL-2026-${num}`,
      asunto: `Solicitud de obra pública número ${num}`,
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Digital',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: `2026-06-${String((i % 20) + 1).padStart(2, '0')}`,
      fechaVencimiento: `2026-07-${String((i % 25) + 1).padStart(2, '0')}`,
      diasRestantes: i + 1,
      version: `v-${num}`,
    };
  });

  const ctx = await setupV04Routes(page, { initialItems: twentyFiveItems });
  await page.goto('/admin/solicitudes');

  // Orden predeterminado: vencimiento asc, página 1, tamaño 10
  await expect(page.getByText('Mostrando 1 a 10 de 25 solicitudes')).toBeVisible();
  expect(ctx.seenListUrls[0].searchParams.get('sort')).toBe('vencimiento');
  expect(ctx.seenListUrls[0].searchParams.get('dir')).toBe('asc');
  expect(ctx.seenListUrls[0].searchParams.get('size')).toBe('10');

  // Cambiar orden a Folio asc y luego Folio desc
  await page.getByRole('button', { name: 'Folio' }).click();
  await expect(page).toHaveURL(/sort=folio&dir=asc/);
  await expect(page.locator('thead th.v04-col-folio')).toHaveAttribute(
    'aria-sort',
    'ascending',
  );

  await page.getByRole('button', { name: 'Folio' }).click();
  await expect(page).toHaveURL(/sort=folio&dir=desc/);
  await expect(page.locator('thead th.v04-col-folio')).toHaveAttribute(
    'aria-sort',
    'descending',
  );
  await expect(page.locator('tbody tr').first()).toContainText('SOL-2026-025');

  // Navegar a página 2
  await page.getByRole('button', { name: 'Página 2' }).click();
  await expect(page).toHaveURL(/page=2/);
  await expect(page.getByText('Mostrando 11 a 20 de 25 solicitudes')).toBeVisible();

  // Cambiar tamaño de página a 25 -> restablece page a 1 conservando sort=folio&dir=desc
  await page.getByLabel('Solicitudes por página').selectOption('25');
  await expect(page).toHaveURL(/size=25/);
  await expect(page).toHaveURL(/sort=folio&dir=desc/);
  await expect(page).not.toHaveURL(/page=2/);
  await expect(page.getByText('Mostrando 1 a 25 de 25 solicitudes')).toBeVisible();

  // Cambiar a 50 y de vuelta a 10, ir a página 2 y buscar -> restablece page a 1 conservando sort, dir y size
  await page.getByLabel('Solicitudes por página').selectOption('50');
  await expect(page).toHaveURL(/size=50/);

  await page.getByLabel('Solicitudes por página').selectOption('10');
  await page.getByRole('button', { name: 'Página 2' }).click();
  await expect(page).toHaveURL(/page=2/);

  const searchBox = page.getByPlaceholder('Buscar por folio o fecha (dd/mm/aaaa)');
  await searchBox.fill('SOL-2026-00');
  await searchBox.press('Enter');
  await expect(page).not.toHaveURL(/page=2/);
  await expect(page).toHaveURL(/sort=folio&dir=desc/);
  await expect(page.getByText('Mostrando 1 a 9 de 9 solicitudes')).toBeVisible();
});

test('estados vacíos: sin solicitudes registradas vs sin coincidencias, y navegación a Nueva, Editar y Detalle de solo lectura', async ({
  page,
}) => {
  // 1. Estado vacío sin solicitudes registradas
  await setupV04Routes(page, { initialItems: [] });
  await page.goto('/admin/solicitudes');
  await expect(page.getByTestId('empty-no-solicitudes')).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'Aún no hay solicitudes registradas.' }),
  ).toBeVisible();

  await page.getByRole('button', { name: 'Nueva solicitud' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes\/nueva$/);

  // 2. Con registros: navegación a Ver (consulta mínima) y Editar
  await page.unrouteAll();
  await setupV04Routes(page);
  await page.goto('/admin/solicitudes');
  await expect(page.getByRole('cell', { name: '00045-A/2026', exact: true })).toBeVisible();

  // Acción Ver -> /admin/solicitudes/:folio (solo lectura, con — en Entregada)
  await page.getByRole('button', { name: 'Ver solicitud 00045-A/2026' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes\/00045-A%2F2026$/);
  await expect(
    page.getByRole('heading', { name: 'Detalle de solicitud', level: 1 }),
  ).toBeVisible();
  await expect(
    page.getByText('Construcción de guarniciones y banquetas en colonia centro'),
  ).toBeVisible();
  await expect(page.locator('.v04-estatus-badge')).toHaveText('Entregada');
  await expect(page.locator('.v04-dias-dash')).toHaveText('—');
  // Solo lectura: no debe haber botón Guardar cambios ni inputs editables
  await expect(page.getByRole('button', { name: /Guardar/i })).toHaveCount(0);

  await page.getByRole('button', { name: 'Volver al listado' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  // Acción Editar -> /admin/solicitudes/:folio/editar
  await page.getByRole('button', { name: 'Editar solicitud 00045-A/2026' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes\/00045-A%2F2026\/editar$/);
});

test('vista de consulta mínima (/admin/solicitudes/:folio): estados 404 y B-04 con Reintentar', async ({
  page,
}) => {
  let shouldFail503 = true;
  await setupV04Routes(page, {
    onGetEdicion: (route, folio) => {
      if (folio === 'NO-EXISTE-999') {
        return route.fulfill({ status: 404, json: { title: 'Not Found' } });
      }
      if (shouldFail503) {
        return route.fulfill({ status: 503, json: { title: 'Service Unavailable' } });
      }
      return route.fulfill({
        status: 200,
        headers: { ETag: '"ver-001-a"' },
        json: {
          id: 'sol-001',
          folio: 'SOL-2025-001',
          fechaEntrega: '2025-06-10',
          horaEntrega: '09:00',
          areaReceptoraId: 'direccion-obras-publicas',
          asunto: 'Rehabilitación de pavimento hidráulico',
          medioEntrega: 'Digital',
          estatusEntrega: 'EnProceso',
          tipoSolicitud: 'Informacion',
          tipoEvidencia: 'Carpeta',
          tieneAcuse: false,
          tipoAcuse: 'NoAplica',
          fechaVencimiento: '2025-06-30',
          diasRestantes: 13,
          estatusEfectivo: 'EnProceso',
        },
      });
    },
  });

  await page.goto('/admin/solicitudes/NO-EXISTE-999');
  await expect(page.getByTestId('detalle-not-found')).toBeVisible();
  await expect(page.getByText('Solicitud no disponible')).toBeVisible();
  await page.getByRole('button', { name: 'Volver al listado' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  await page.goto('/admin/solicitudes/SOL-2025-001');
  await expect(page.getByRole('alert')).toContainText('No se pudo cargar la solicitud.');
  shouldFail503 = false;
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByText('Rehabilitación de pavimento hidráulico')).toBeVisible();
  await expect(page.locator('.v04-dias-chip.is-green')).toHaveText('13 días');
});

test('modal M-04: Cancelar, Escape, estado ocupado, eliminación 204 con If-Match strong ETag, toast y retroceso de página vacía', async ({
  page,
}) => {
  // 11 registros -> página 1 tiene 10, página 2 tiene 1 solo registro
  const elevenItems: MockListadoItem[] = Array.from({ length: 11 }, (_, idx) => {
    const n = String(idx + 1).padStart(3, '0');
    return {
      id: `id-${n}`,
      folio: `SOL-DEL-${n}`,
      asunto: `Asunto de prueba ${n}`,
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Digital',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: '2026-06-01',
      fechaVencimiento: `2026-07-${String(idx + 1).padStart(2, '0')}`,
      diasRestantes: 10,
      version: `raw-token-${n}`,
    };
  });

  let releaseDelete!: () => void;
  const deleteGate = new Promise<void>((resolve) => {
    releaseDelete = resolve;
  });
  let capturedIfMatch = '';

  const ctx = await setupV04Routes(page, {
    initialItems: elevenItems,
    onDeleteSolicitud: async (route, id, headers, _count, store) => {
      capturedIfMatch = headers['if-match'] ?? '';
      await deleteGate;
      const pos = store.findIndex((i) => i.id === id);
      if (pos >= 0) store.splice(pos, 1);
      return route.fulfill({ status: 204 });
    },
  });

  await page.goto('/admin/solicitudes?page=2');
  await expect(page.getByText('Mostrando 11 a 11 de 11 solicitudes')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'SOL-DEL-011', exact: true })).toBeVisible();

  // Abrir M-04: no envía DELETE antes de confirmar; Cancelar y Escape cierran sin borrar
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-DEL-011' }).click();
  const modal = page.getByRole('dialog');
  await expect(modal.getByRole('heading', { name: '¿Eliminar solicitud?' })).toBeVisible();
  await expect(modal.locator('strong')).toHaveText('SOL-DEL-011');
  expect(ctx.getDeleteCount()).toBe(0);

  await modal.getByRole('button', { name: 'Cancelar' }).click();
  await expect(modal).toHaveCount(0);
  expect(ctx.getDeleteCount()).toBe(0);

  await page.getByRole('button', { name: 'Eliminar solicitud SOL-DEL-011' }).click();
  await expect(modal).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(modal).toHaveCount(0);
  expect(ctx.getDeleteCount()).toBe(0);

  // Confirmar eliminación: botones deshabilitados y fila aún en DOM durante petición en vuelo
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-DEL-011' }).click();
  await modal.getByRole('button', { name: 'Eliminar' }).click();

  await expect(modal.getByRole('button', { name: 'Cancelar' })).toBeDisabled();
  await expect(modal.getByRole('button', { name: 'Eliminando…' })).toBeDisabled();
  await expect(page.locator('tbody tr', { hasText: 'SOL-DEL-011' })).toHaveCount(1);

  releaseDelete();

  // Cierra modal, formatea If-Match con comillas dobles, retrocede a página 1 y muestra toast
  await expect(modal).toHaveCount(0);
  expect(capturedIfMatch).toBe('"raw-token-011"');
  await expect(page).not.toHaveURL(/page=2/);
  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Solicitud SOL-DEL-011 eliminada.',
  );
  await expect(page.getByText('Mostrando 1 a 10 de 10 solicitudes')).toBeVisible();
});

test('modal M-04 errores controlados: 409 integridad, 412 conflicto de concurrencia, 404 inexistente y 503 servicio no disponible', async ({
  page,
}) => {
  let mode: '409' | '412' | '503' | '404' = '409';

  await setupV04Routes(page, {
    onDeleteSolicitud: (route, id, _headers, _count, store) => {
      if (mode === '409') {
        return route.fulfill({
          status: 409,
          json: { title: 'Integrity conflict' },
        });
      }
      if (mode === '412') {
        const found = store.find((i) => i.id === id);
        if (found) {
          found.asunto = 'Asunto modificado por otro administrador';
          found.version = 'ver-001-updated';
        }
        return route.fulfill({
          status: 412,
          json: { title: 'Precondition Failed' },
        });
      }
      if (mode === '503') {
        return route.fulfill({
          status: 503,
          json: { title: 'Service Unavailable' },
        });
      }
      const pos = store.findIndex((i) => i.id === id);
      if (pos >= 0) store.splice(pos, 1);
      return route.fulfill({
        status: 404,
        json: { title: 'Not Found' },
      });
    },
  });

  await page.goto('/admin/solicitudes');
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  // 1. 409 Integridad referencial: conserva fila y explica motivo
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'existen datos relacionados que impiden eliminarla',
  );
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  // 2. 412 Precondición fallida: informa cambio concurrente y ofrece Actualizar listado
  mode = '412';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'cambió desde que fue consultada',
  );
  await page.getByRole('button', { name: 'Actualizar listado' }).click();
  await expect(page.getByText('Asunto modificado por otro administrador')).toBeVisible();

  // 3. 503 Servicio no disponible: conserva fila y avisa motivo controlado
  mode = '503';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'servicio no está disponible temporalmente',
  );
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  // 4. 404 Ya no existe: retira fila fantasma e informa que ya no existe permitiendo actualizar
  mode = '404';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('delete-feedback-banner')).toContainText('ya no existe');
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Actualizar listado' }).click();
  await expect(page.getByText('Mostrando 1 a 3 de 3 solicitudes')).toBeVisible();
});

test('pérdida de respuesta de red en DELETE: verifica con obtenerSolicitudParaEdicion (200 igual ETag, 200 distinto ETag, fallo red y 404 borrado confirmado)', async ({
  page,
}) => {
  let scenario: 'still-same' | 'etag-changed' | 'verify-network-fail' | 'deleted-404' =
    'still-same';

  const ctx = await setupV04Routes(page, {
    onDeleteSolicitud: async (route, id, _headers, _count, store) => {
      if (scenario === 'deleted-404') {
        const idx = store.findIndex((i) => i.id === id);
        if (idx >= 0) store.splice(idx, 1);
      } else if (scenario === 'etag-changed') {
        const found = store.find((i) => i.id === id);
        if (found) found.version = 'ver-001-mutated';
      }
      return route.abort('connectionreset');
    },
    onGetEdicion: (route, folio, _count, store) => {
      if (scenario === 'verify-network-fail') {
        return route.abort('connectionfailed');
      }
      const found = store.find((i) => i.folio === folio);
      if (!found) {
        return route.fulfill({ status: 404, json: { title: 'Not Found' } });
      }
      const etag = found.version.startsWith('"') ? found.version : `"${found.version}"`;
      return route.fulfill({
        status: 200,
        headers: { ETag: etag },
        json: {
          id: found.id,
          folio: found.folio,
          fechaEntrega: found.fechaEntrega,
          horaEntrega: '10:00',
          areaReceptoraId: 'direccion-obras-publicas',
          asunto: found.asunto,
          medioEntrega: found.medioEntrega,
          estatusEntrega: 'EnProceso',
          tipoSolicitud: 'Informacion',
          tipoEvidencia: 'Carpeta',
          tieneAcuse: false,
          tipoAcuse: 'NoAplica',
          fechaVencimiento: found.fechaVencimiento,
          diasRestantes: found.diasRestantes,
          estatusEfectivo: found.estatusEfectivo,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes');
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  // Caso A: DELETE cae por red, GET verificación responde 200 con MISMO ETag -> conserva fila sin anunciar éxito
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('solicitud-toast')).toHaveCount(0);
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'continúa registrada',
  );
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();
  expect(ctx.getEdicionCount()).toBe(1);

  // Caso B: DELETE cae por red, verificación también falla por red -> mantiene fila sin anunciar éxito e invita a actualizar
  scenario = 'verify-network-fail';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('solicitud-toast')).toHaveCount(0);
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'No se pudo confirmar el resultado',
  );
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  // Caso C: DELETE cae por red, GET verificación responde 200 con ETag distinto -> conflicto de concurrencia
  scenario = 'etag-changed';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('solicitud-toast')).toHaveCount(0);
  await expect(page.getByTestId('delete-feedback-banner')).toContainText(
    'cambió desde que fue consultada',
  );
  await page.getByRole('button', { name: 'Actualizar listado' }).click();

  // Caso D: DELETE sí se aplicó en servidor pero cayó la red -> GET verificación responde 404 -> confirma eliminación y muestra toast
  scenario = 'deleted-404';
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Solicitud SOL-2025-001 eliminada.',
  );
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toHaveCount(0);
  await expect(page.getByText('Mostrando 1 a 3 de 3 solicitudes')).toBeVisible();
});

test('error de carga B-04 durante paginación y reingreso SPA: conserva criterios, permite Reintentar y jamás muestra datos obsoletos', async ({
  page,
}) => {
  const fifteenItems: MockListadoItem[] = Array.from({ length: 15 }, (_, idx) => {
    const n = String(idx + 1).padStart(3, '0');
    return {
      id: `item-${n}`,
      folio: `SOL-B04-${n}`,
      asunto: `Solicitud ${n}`,
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Digital',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: '2026-06-01',
      fechaVencimiento: `2026-07-${String(idx + 1).padStart(2, '0')}`,
      diasRestantes: 9,
      version: `v-${n}`,
    };
  });

  let shouldFailPage2 = true;
  let releasePage2!: () => void;
  const page2Gate = new Promise<void>((resolve) => {
    releasePage2 = resolve;
  });

  await setupV04Routes(page, {
    initialItems: fifteenItems,
    onGetSolicitudes: async (route, url, _count, store) => {
      const p = Number(url.searchParams.get('page') ?? '1');
      if (p === 2 && shouldFailPage2) {
        await page2Gate;
        return route.fulfill({ status: 503, json: { title: 'Service Unavailable' } });
      }
      const start = (p - 1) * 10;
      return route.fulfill({
        status: 200,
        json: {
          items: store.slice(start, start + 10),
          page: p,
          pageSize: 10,
          totalItems: store.length,
          totalPages: 2,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes?dias=8%2B');
  await expect(page.getByRole('cell', { name: 'SOL-B04-001', exact: true })).toBeVisible();
  await expect(page.getByTestId('filter-chip-dias')).toContainText('Días restantes: 8 o más');

  // Cambiar a página 2: mientras carga mantiene controles de paginación accesibles, indicador de carga y skeleton sin datos viejos
  await page.getByRole('button', { name: 'Página 2' }).click();
  await expect(page.getByTestId('solicitudes-loading-indicator')).toHaveText(
    'Cargando solicitudes…',
  );
  await expect(page.getByTestId('solicitud-skeleton-row')).toHaveCount(6);
  await expect(page.getByRole('cell', { name: 'SOL-B04-001', exact: true })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Paginación de solicitudes' })).toBeVisible();

  // Falla con 503 -> muestra B-04, conserva criterio dias=8+ y page=2, y NO muestra filas antiguas de página 1
  releasePage2();
  await expect(page.getByRole('alert')).toContainText(
    'No se pudo cargar la lista de solicitudes.',
  );
  await expect(page.getByRole('cell', { name: 'SOL-B04-001', exact: true })).toHaveCount(0);
  await expect(page.getByTestId('filter-chip-dias')).toContainText('Días restantes: 8 o más');
  await expect(page).toHaveURL(/page=2/);

  // Reintentar recupera página 2
  shouldFailPage2 = false;
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByRole('cell', { name: 'SOL-B04-011', exact: true })).toBeVisible();
});

test('responsive: 1280px scroll horizontal + Folio sticky, 1024px buscador fila completa y 768px tarjetas + FAB de 56px', async ({
  page,
}) => {
  await setupV04Routes(page);

  // 1. <= 1280px: scroll horizontal y columna Folio sticky
  await page.setViewportSize({ width: 1200, height: 800 });
  await page.goto('/admin/solicitudes');
  await expect(page.getByRole('cell', { name: 'SOL-2025-001', exact: true })).toBeVisible();

  const folioCell = page.locator('tbody td.v04-col-folio').first();
  await expect(folioCell).toHaveCSS('position', 'sticky');
  await expect(folioCell).toHaveCSS('left', '0px');

  const hasHorizontalScroll = await page
    .locator('.v04-table-scroll')
    .evaluate((scroller) => {
      const scrollable = scroller as unknown as {
        scrollWidth: number;
        clientWidth: number;
        scrollLeft: number;
      };
      const canScroll = scrollable.scrollWidth > scrollable.clientWidth;
      scrollable.scrollLeft = 180;
      return canScroll;
    });
  expect(hasHorizontalScroll).toBe(true);

  // 2. <= 1024px: buscador en primera fila completa y botones en segunda fila
  await page.setViewportSize({ width: 1024, height: 800 });
  const searchRect = await page.locator('.v04-search-box').boundingBox();
  const actionsRect = await page.locator('.v04-toolbar-actions').boundingBox();
  expect(searchRect).not.toBeNull();
  expect(actionsRect).not.toBeNull();
  expect(Math.round(actionsRect!.y)).toBeGreaterThanOrEqual(
    Math.round(searchRect!.y + searchRect!.height),
  );
  expect(Math.abs(searchRect!.width - actionsRect!.width)).toBeLessThanOrEqual(2);

  // 3. <= 768px: tarjetas en lugar de tabla + FAB circular de 56px con aria-label="Nueva solicitud"
  await page.setViewportSize({ width: 768, height: 844 });
  await expect(page.locator('table.v04-table')).toHaveCount(0);
  await expect(page.getByTestId('solicitud-mobile-card')).toHaveCount(4);

  const fabButton = page.getByRole('button', { name: 'Nueva solicitud' });
  await expect(fabButton).toBeVisible();
  const fabBox = await fabButton.boundingBox();
  expect(Math.round(fabBox!.width)).toBe(56);
  expect(Math.round(fabBox!.height)).toBe(56);
});

test('botón Limpiar dentro del popover conserva búsqueda q, navegación AdminShell y expiración 401 en DELETE redirige a login', async ({
  page,
}) => {
  const ctx = await setupV04Routes(page, {
    onDeleteSolicitud: (route) => {
      ctx.setRoles(null);
      return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
    },
  });

  await page.goto('/admin/solicitudes?q=SOL-2025&dias=8%2B');
  await expect(page.getByTestId('filter-chip-dias')).toContainText('Días restantes: 8 o más');

  // Limpiar desde dentro del popover limpia los filtros pero conserva q en el buscador
  await page.getByRole('button', { name: /^Filtros/ }).click();
  const popover = page.getByRole('dialog', { name: 'Filtros de solicitudes' });
  await expect(popover.getByLabel('Días restantes')).toHaveValue('8+');
  await popover.getByRole('button', { name: 'Limpiar' }).click();
  await expect(popover.getByLabel('Días restantes')).toHaveValue('todos');
  await expect(page).not.toHaveURL(/dias=/);
  await expect(page).toHaveURL(/q=SOL-2025/);
  await page.keyboard.press('Escape');

  // Navegación desde Detalle hacia Solicitudes mediante botón Solicitudes de AdminShell
  await page.getByRole('button', { name: 'Ver solicitud SOL-2025-001' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes\/SOL-2025-001$/);
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  // Expiración 401 durante DELETE invalida sesión y redirige a /admin/login
  await page.getByRole('button', { name: 'Eliminar solicitud SOL-2025-001' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);
});

test('navegación AdminShell botón Solicitudes desde NuevaSolicitudPage y EditarSolicitudPage (limpia y con confirmación de descarte)', async ({
  page,
}) => {
  await setupV04Routes(page);

  // 1. NuevaSolicitudPage limpia -> navega directo a /admin/solicitudes
  await page.goto('/admin/solicitudes/nueva');
  await expect(page.getByRole('heading', { name: 'Nueva solicitud', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  // 2. NuevaSolicitudPage sucia -> abre ¿Descartar cambios?; Seguir editando conserva destino /admin/dashboard para Cancelar
  await page.goto('/admin/solicitudes/nueva');
  await page.getByLabel(/Asunto corto/i).fill('Borrador temporal sin guardar');
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  const modalNueva = page.getByRole('dialog');
  await expect(modalNueva).toContainText('¿Descartar cambios?');
  await modalNueva.getByRole('button', { name: 'Seguir editando' }).click();
  await expect(modalNueva).toHaveCount(0);

  // Al pulsar Cancelar -> Descartar sigue yendo a /admin/dashboard intacto
  await page.getByRole('button', { name: 'Cancelar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);

  // NuevaSolicitudPage sucia -> Solicitudes -> Descartar navega a /admin/solicitudes
  await page.goto('/admin/solicitudes/nueva');
  await page.getByLabel(/Asunto corto/i).fill('Segundo borrador para descartar hacia lista');
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  // 3. EditarSolicitudPage limpia -> navega directo a /admin/solicitudes
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas en colonia centro',
  );
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);

  // 4. EditarSolicitudPage sucia -> Solicitudes -> Descartar navega a /admin/solicitudes
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas en colonia centro',
  );
  await page.getByLabel(/Asunto corto/i).fill('Modificación sin guardar en edición');
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  const modalEditar = page.getByRole('dialog');
  await expect(modalEditar).toContainText('¿Descartar cambios?');
  await modalEditar.getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);
});

test('geometría sin desbordamiento en th.v04-col-dias (100px) y td.v04-col-acciones (112px), cancelación de debounce y redirección de página huérfana', async ({
  page,
}) => {
  const fifteenItems: MockListadoItem[] = Array.from({ length: 15 }, (_, idx) => {
    const num = String(idx + 1).padStart(3, '0');
    return {
      id: `geom-${num}`,
      folio: `SOL-GEO-${num}`,
      asunto: `Asunto geométrico ${num}`,
      areaReceptora: 'Dirección de Obras Públicas',
      medioEntrega: 'Digital',
      estatusEfectivo: 'EnProceso',
      fechaEntrega: '2026-06-01',
      fechaVencimiento: `2026-07-${String(idx + 1).padStart(2, '0')}`,
      diasRestantes: 12,
      version: `vg-${num}`,
    };
  });

  await setupV04Routes(page, { initialItems: fifteenItems });
  await page.setViewportSize({ width: 1440, height: 900 });

  // 1. Página huérfana (page=5 > totalPages=2) redirige automáticamente con replace a page=2
  await page.goto('/admin/solicitudes?sort=folio&dir=asc&page=5');
  await expect(page).toHaveURL(/sort=folio&dir=asc&page=2$/);
  await expect(page.getByText('Mostrando 11 a 15 de 15 solicitudes')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'SOL-GEO-011', exact: true })).toBeVisible();

  // 2. Aserción geométrica getBoundingClientRect() en th.v04-col-dias y td.v04-col-acciones
  const readRect = async (selector: string) =>
    page.locator(selector).first().evaluate((el) => {
      const box = (
        el as unknown as {
          getBoundingClientRect: () => {
            left: number;
            right: number;
            width: number;
            height: number;
          };
        }
      ).getBoundingClientRect();
      return {
        left: box.left,
        right: box.right,
        width: box.width,
        height: box.height,
      };
    });

  const thDiasRect = await readRect('thead th.v04-col-dias');
  const sortDiasBtnRect = await readRect('thead th.v04-col-dias .v04-sort-btn');
  const thAccionesRect = await readRect('thead th.v04-col-acciones');
  const tdAccionesRect = await readRect('tbody td.v04-col-acciones');
  const rowActionsRect = await readRect('tbody td.v04-col-acciones .v04-row-actions');

  expect(Math.round(thDiasRect.width)).toBe(100);
  expect(Math.round(tdAccionesRect.width)).toBe(112);
  // El botón de ordenamiento de Días restantes no invade th.v04-col-acciones
  expect(sortDiasBtnRect.right).toBeLessThanOrEqual(thDiasRect.right + 0.5);
  expect(sortDiasBtnRect.right).toBeLessThanOrEqual(thAccionesRect.left + 0.5);
  // Los 3 botones de 32x32 (.v04-row-actions) caben íntegramente dentro de td.v04-col-acciones (112px)
  expect(rowActionsRect.left).toBeGreaterThanOrEqual(tdAccionesRect.left - 0.5);
  expect(rowActionsRect.right).toBeLessThanOrEqual(tdAccionesRect.right + 0.5);

  // 3. Cancelación de debounce pendiente al pulsar botón Solicitudes en AdminShell
  const searchBox = page.getByPlaceholder('Buscar por folio o fecha (dd/mm/aaaa)');
  await searchBox.fill('NO-DEBE-APLICARSE');
  await page.getByRole('button', { name: 'Solicitudes' }).click();
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);
  await expect(searchBox).toHaveValue('');
  await page.waitForTimeout(380);
  await expect(page).toHaveURL(/\/admin\/solicitudes$/);
  await expect(searchBox).toHaveValue('');
});
