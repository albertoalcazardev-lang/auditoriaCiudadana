import { expect, test, type Page, type Route } from '@playwright/test';
import { randomUUID } from 'node:crypto';

const MOCK_CATALOGOS = {
  areasReceptoras: [
    { id: 'direccion-obras-publicas', nombre: 'Dirección de Obras Públicas' },
    { id: 'secretaria-finanzas', nombre: 'Secretaría de Finanzas' },
    { id: 'contraloria-municipal', nombre: 'Contraloría Municipal' },
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
    { codigo: 'Otro', nombre: 'Otro' },
  ],
  tiposEvidencia: [
    { codigo: 'Carpeta', nombre: 'Carpeta' },
    { codigo: 'Link', nombre: 'Link' },
  ],
  tiposAcuse: [
    { codigo: 'Sello', nombre: 'Sello' },
    { codigo: 'Folio', nombre: 'Folio' },
    { codigo: 'Electronico', nombre: 'Electrónico' },
    { codigo: 'NoAplica', nombre: 'No aplica' },
  ],
};

function getMexicoCityDateOffset(offsetDays: number): { ddMmYyyy: string; iso: string } {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'America/Mexico_City',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date());
  const lookup = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  const baseUtc = new Date(
    Date.UTC(Number(lookup.year), Number(lookup.month) - 1, Number(lookup.day) + offsetDays),
  );
  const dd = String(baseUtc.getUTCDate()).padStart(2, '0');
  const mm = String(baseUtc.getUTCMonth() + 1).padStart(2, '0');
  const yyyy = String(baseUtc.getUTCFullYear());
  return { ddMmYyyy: `${dd}/${mm}/${yyyy}`, iso: `${yyyy}-${mm}-${dd}` };
}

interface MockSolicitudEdicion {
  id: string;
  folio: string;
  fechaEntrega: string;
  horaEntrega: string;
  areaReceptoraId: string;
  asunto: string;
  medioEntrega: string;
  estatusEntrega: string;
  tipoSolicitud: string;
  tipoEvidencia: string;
  tieneAcuse: boolean;
  tipoAcuse: string;
  fechaVencimiento: string;
  diasRestantes: number;
  estatusEfectivo: string;
}

function buildMockSolicitud(overrides: Partial<MockSolicitudEdicion> = {}): MockSolicitudEdicion {
  const vencimiento = getMexicoCityDateOffset(8);
  return {
    id: 'sol-edit-001',
    folio: '00045-A/2026',
    fechaEntrega: '2025-06-10',
    horaEntrega: '09:15',
    areaReceptoraId: 'direccion-obras-publicas',
    asunto: 'Construcción de guarniciones y banquetas',
    medioEntrega: 'Impreso',
    estatusEntrega: 'Entregada',
    tipoSolicitud: 'Informacion',
    tipoEvidencia: 'Link',
    tieneAcuse: true,
    tipoAcuse: 'Folio',
    fechaVencimiento: vencimiento.iso,
    diasRestantes: 8,
    estatusEfectivo: 'Entregada',
    ...overrides,
  };
}

async function setupEditarRoutes(
  page: Page,
  options: {
    roles?: string[] | null;
    initialEtag?: string;
    initialSolicitud?: MockSolicitudEdicion;
    onCatalogos?: (route: Route, count: number) => Promise<void> | void;
    onGetEdicion?: (route: Route, count: number, folio: string | null) => Promise<void> | void;
    onPutSolicitud?: (route: Route, count: number, id: string) => Promise<void> | void;
    onPostSolicitud?: (route: Route, count: number) => Promise<void> | void;
  } = {},
) {
  const {
    roles = ['Administrador'],
    initialEtag = '"etag-v1-85eda206"',
    initialSolicitud = buildMockSolicitud(),
    onCatalogos,
    onGetEdicion,
    onPutSolicitud,
    onPostSolicitud,
  } = options;

  let csrfToken = randomUUID();
  let catalogosCount = 0;
  let getEdicionCount = 0;
  let putCount = 0;
  let postCount = 0;

  await page.route('https://localhost:5173/api/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/auth/me') {
      if (!roles) return route.fulfill({ status: 401 });
      return route.fulfill({
        json: { userId: 'admin-1', userName: 'Ana Administradora', roles },
      });
    }

    if (path === '/api/auth/csrf') {
      csrfToken = randomUUID();
      return route.fulfill({ json: { requestToken: csrfToken } });
    }

    if (path === '/api/auth/logout' && request.method() === 'POST') {
      return route.fulfill({ status: 204 });
    }

    if (path === '/api/solicitudes/catalogos' && request.method() === 'GET') {
      catalogosCount += 1;
      if (onCatalogos) return onCatalogos(route, catalogosCount);
      return route.fulfill({ json: MOCK_CATALOGOS });
    }

    if (path === '/api/solicitudes/edicion' && request.method() === 'GET') {
      getEdicionCount += 1;
      const folioQuery = url.searchParams.get('folio');
      if (onGetEdicion) return onGetEdicion(route, getEdicionCount, folioQuery);
      return route.fulfill({
        status: 200,
        headers: { ETag: initialEtag },
        json: {
          ...initialSolicitud,
          folio: folioQuery ?? initialSolicitud.folio,
        },
      });
    }

    if (path.startsWith('/api/solicitudes/') && request.method() === 'PUT') {
      const headerCsrf = request.headers()['x-csrf-token'];
      if (!headerCsrf || headerCsrf !== csrfToken) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }
      putCount += 1;
      const id = decodeURIComponent(path.replace('/api/solicitudes/', ''));
      if (onPutSolicitud) return onPutSolicitud(route, putCount, id);

      const body = request.postDataJSON();
      return route.fulfill({
        status: 200,
        headers: { ETag: `"etag-v${putCount + 1}"` },
        json: {
          id,
          folio: initialSolicitud.folio,
          ...body,
          diasRestantes: 14,
          estatusEfectivo: body.estatusEntrega,
        },
      });
    }

    if (path === '/api/solicitudes' && request.method() === 'POST') {
      const headerCsrf = request.headers()['x-csrf-token'];
      if (!headerCsrf || headerCsrf !== csrfToken) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }
      postCount += 1;
      if (onPostSolicitud) return onPostSolicitud(route, postCount);
      const body = request.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: 'sol-created-99',
          folio: body.generarFolioAutomatico ? '00099-B/2026' : body.folio,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    }

    return route.abort();
  });
}

test('acceso administrativo, redirección de anónimo y Acceso denegado para Consulta sin consultar API de edición', async ({
  page,
}) => {
  await setupEditarRoutes(page, { roles: null });
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page).toHaveURL(/\/admin\/login$/);

  await page.unrouteAll();
  let apiTouchedByConsulta = false;
  await setupEditarRoutes(page, {
    roles: ['Consulta'],
    onCatalogos: (route) => {
      apiTouchedByConsulta = true;
      return route.fulfill({ json: MOCK_CATALOGOS });
    },
    onGetEdicion: (route) => {
      apiTouchedByConsulta = true;
      return route.fulfill({ status: 200, headers: { ETag: '"v1"' }, json: buildMockSolicitud() });
    },
  });
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Ir a mi panel' })).toBeVisible();
  expect(apiTouchedByConsulta).toBe(false);
});

test('skeleton de 12 controles durante precarga, prefers-reduced-motion, carga de folio 00045-A/2026, folio inmutable y Eliminar deshabilitado', async ({
  page,
}) => {
  let releaseEdicion!: () => void;
  const edicionGate = new Promise<void>((resolve) => {
    releaseEdicion = resolve;
  });

  let requestedFolio: string | null = null;
  const vencimiento = getMexicoCityDateOffset(8);

  await setupEditarRoutes(page, {
    onGetEdicion: async (route, _count, folioQuery) => {
      requestedFolio = folioQuery;
      await edicionGate;
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-init-00045"' },
        json: buildMockSolicitud({
          folio: '00045-A/2026',
          fechaEntrega: '2025-06-10',
          horaEntrega: '09:15',
          fechaVencimiento: vencimiento.iso,
          diasRestantes: 8,
        }),
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);

  // Verificar AdminShell
  await expect(page.getByRole('heading', { name: 'Editar solicitud', level: 1 })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText(
    'Solicitudes›Editar solicitud',
  );

  // Verificar Skeleton de los 12 controles (40px de alto) y Guardar cambios deshabilitado
  const skeletonControls = page.getByTestId('solicitud-skeleton-control');
  await expect(skeletonControls).toHaveCount(12);
  const firstHeight = await skeletonControls
    .first()
    .evaluate((el) => el.getBoundingClientRect().height);
  expect(firstHeight).toBe(40);
  await expect(page.getByRole('button', { name: /Guardar cambios/i })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Eliminar' })).toBeDisabled();

  // Verificar respeto de prefers-reduced-motion en skeleton
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const animationName = await skeletonControls
    .first()
    .evaluate((el) => el.ownerDocument.defaultView?.getComputedStyle(el).animationName);
  expect(animationName).toBe('none');
  await page.emulateMedia({ reducedMotion: 'no-preference' });

  // Liberar respuesta y verificar valores vigentes (no fecha/hora actuales)
  releaseEdicion();

  await expect(skeletonControls).toHaveCount(0);
  expect(requestedFolio).toBe('00045-A/2026');

  // Chip de folio inmutable y ausencia de controles de edición/autogeneración de folio
  const folioChip = page.getByTestId('immutable-folio-chip');
  await expect(folioChip).toHaveText('Folio 00045-A/2026');
  const chipStyles = await folioChip.evaluate((el) => {
    const cs = el.ownerDocument.defaultView!.getComputedStyle(el);
    return {
      height: el.getBoundingClientRect().height,
      bg: cs.backgroundColor,
      color: cs.color,
      fontWeight: cs.fontWeight,
    };
  });
  expect(chipStyles.height).toBe(28);
  expect(chipStyles.bg).toBe('rgb(234, 241, 255)'); // #EAF1FF
  expect(chipStyles.color).toBe('rgb(29, 78, 216)'); // #1D4ED8
  expect(chipStyles.fontWeight).toBe('600');

  await expect(page.locator('#solicitud-folio')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Generar folio automático/i })).toHaveCount(0);

  // Valores precargados del servidor
  await expect(page.getByLabel(/Fecha de entrega/i)).toHaveValue('10/06/2025');
  await expect(page.getByLabel(/Hora de entrega/i)).toHaveValue('09:15');
  await expect(
    page.getByRole('combobox', { name: /Área o dependencia receptora/i }),
  ).toHaveValue('Dirección de Obras Públicas');
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );
  await expect(page.locator('#asunto-counter')).toHaveText('40/100');
  await expect(page.getByLabel(/Medio de entrega/i)).toHaveValue('Impreso');
  await expect(page.getByLabel(/Estatus de entrega/i)).toHaveValue('Entregada');
  await expect(page.getByLabel(/Tipo de solicitud/i)).toHaveValue('Informacion');
  await expect(page.getByLabel(/Evidencia que se lleva a cabo/i)).toHaveValue('Link');
  await expect(page.getByLabel(/¿Tiene acuse\?/i)).toHaveValue('si');
  await expect(page.getByLabel(/Tipo de acuse/i)).toHaveValue('Folio');
  await expect(page.getByLabel(/Plazo de vencimiento/i)).toHaveValue(vencimiento.ddMmYyyy);

  const diasControl = page.getByRole('status').filter({ hasText: /Días restantes:/ });
  await expect(diasControl).toContainText('Días restantes: 8 días');
  await expect(diasControl).toHaveAttribute('data-tone', 'normal');

  await expect(page.getByRole('button', { name: 'Eliminar' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeEnabled();
});

test('validaciones completas, asunto 100 vs 101 chars, recálculo de días restantes, foco al primer inválido y conservación de captura', async ({
  page,
}) => {
  let putCalls = 0;
  await setupEditarRoutes(page, {
    onPutSolicitud: (route) => {
      putCalls += 1;
      return route.fulfill({ status: 200, headers: { ETag: '"v2"' }, json: buildMockSolicitud() });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );

  // Asunto 100 chars válido vs 101 chars inválido
  const asunto = page.getByLabel(/Asunto corto/i);
  const counter = page.locator('#asunto-counter');
  await asunto.fill('x'.repeat(100));
  await expect(counter).toHaveText('100/100');
  await expect(counter).toHaveClass(/is-warning/);
  await expect(page.locator('#error-asunto')).toHaveCount(0);

  await asunto.fill('x'.repeat(101));
  await expect(counter).toHaveText('101/100');
  await expect(counter).toHaveClass(/is-error/);
  await expect(page.locator('#error-asunto')).toContainText('no puede exceder 100 caracteres');

  // Regla de acuse No -> NoAplica bloqueado; Sí -> habilitado sin NoAplica
  const tieneAcuse = page.getByLabel(/¿Tiene acuse\?/i);
  const tipoAcuse = page.getByLabel(/Tipo de acuse/i);
  await tieneAcuse.selectOption('no');
  await expect(tipoAcuse).toBeDisabled();
  await expect(tipoAcuse).toHaveValue('NoAplica');

  await tieneAcuse.selectOption('si');
  await expect(tipoAcuse).toBeEnabled();
  await expect(tipoAcuse).toHaveValue('');
  const acuseOptions = await tipoAcuse.locator('option').allTextContents();
  expect(acuseOptions).not.toContain('No aplica');

  // Recálculo visual de Días restantes (>7 verde, 1-7 naranja, 0 rojo, inválida gris)
  const fechaEntrega = page.getByLabel(/Fecha de entrega/i);
  const fechaVencimiento = page.getByLabel(/Plazo de vencimiento/i);
  const diasControl = page.getByRole('status').filter({ hasText: /Días restantes:/ });

  const todayMx = getMexicoCityDateOffset(0);
  await fechaEntrega.fill(todayMx.ddMmYyyy);

  await fechaVencimiento.fill(getMexicoCityDateOffset(12).ddMmYyyy);
  await expect(diasControl).toHaveAttribute('data-tone', 'normal');
  await expect(diasControl).toContainText('Días restantes: 12 días');

  await fechaVencimiento.fill(getMexicoCityDateOffset(5).ddMmYyyy);
  await expect(diasControl).toHaveAttribute('data-tone', 'warning');
  await expect(diasControl).toContainText('Días restantes: 5 días');

  // Fecha igual a entrega es válida y muestra 0 días (rojo)
  await fechaVencimiento.fill(todayMx.ddMmYyyy);
  await fechaVencimiento.blur();
  await expect(page.locator('#error-fecha-vencimiento')).toHaveCount(0);
  await expect(diasControl).toHaveAttribute('data-tone', 'urgent');
  await expect(diasControl).toContainText('Días restantes: 0 días');

  // Fecha anterior a entrega es inválida
  await fechaVencimiento.fill(getMexicoCityDateOffset(-1).ddMmYyyy);
  await fechaVencimiento.blur();
  await expect(page.locator('#error-fecha-vencimiento')).toContainText(
    'no puede ser anterior a la fecha de entrega',
  );

  // Fecha inexistente muestra gris
  await fechaVencimiento.fill('31/02/2026');
  await expect(diasControl).toHaveAttribute('data-tone', 'empty');
  await expect(diasControl).toContainText('Días restantes: —');

  // Provocar múltiples errores (vacíos / formatos) y pulsar Guardar cambios
  await fechaEntrega.fill('');
  await page.getByLabel(/Hora de entrega/i).fill('25:99');
  await asunto.fill('Captura parcial conservada');

  await page.getByRole('button', { name: 'Guardar cambios' }).click();

  await expect(page.getByRole('alert')).toContainText('Revisa los campos marcados (4)');
  await expect(page.locator('#solicitud-fecha-entrega')).toBeFocused();
  await expect(asunto).toHaveValue('Captura parcial conservada');
  await expect(page.getByTestId('immutable-folio-chip')).toHaveText('Folio 00045-A/2026');
  expect(putCalls).toBe(0);
});

test('sin cambios muestra toast No hay cambios por guardar., no abre M-02 y no envía PUT', async ({
  page,
}) => {
  let putCalls = 0;
  await setupEditarRoutes(page, {
    onPutSolicitud: (route) => {
      putCalls += 1;
      return route.fulfill({ status: 200, headers: { ETag: '"v2"' }, json: buildMockSolicitud() });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );

  // Sin tocar ningún campo
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText('No hay cambios por guardar.');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(putCalls).toBe(0);

  // Agregando solo espacios alrededor de asunto (normalización equivalente)
  await page.getByLabel(/Asunto corto/i).fill('  Construcción de guarniciones y banquetas  ');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText('No hay cambios por guardar.');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(putCalls).toBe(0);
});

test('confirmación M-02, envío PUT solo con los 11 campos + If-Match, toast de éxito, datos definitivos del servidor y Seguir editando con nuevo ETag', async ({
  page,
}) => {
  const seenIfMatch: string[] = [];
  const seenBodies: Array<Record<string, unknown>> = [];
  const nuevoVencimiento = getMexicoCityDateOffset(15);

  await setupEditarRoutes(page, {
    initialEtag: '"etag-inicial-1"',
    onPutSolicitud: (route, count, id) => {
      const req = route.request();
      seenIfMatch.push(req.headers()['if-match']);
      const body = req.postDataJSON() as Record<string, unknown>;
      seenBodies.push(body);

      return route.fulfill({
        status: 200,
        headers: { ETag: `"etag-servidor-${count + 1}"` },
        json: {
          id,
          folio: '00045-A/2026',
          fechaEntrega: body.fechaEntrega,
          horaEntrega: body.horaEntrega,
          areaReceptoraId: body.areaReceptoraId,
          asunto: body.asunto,
          medioEntrega: body.medioEntrega,
          estatusEntrega: body.estatusEntrega,
          tipoSolicitud: body.tipoSolicitud,
          tipoEvidencia: body.tipoEvidencia,
          tieneAcuse: body.tieneAcuse,
          tipoAcuse: body.tipoAcuse,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 15,
          estatusEfectivo: 'EnProceso',
        },
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );

  await page.getByLabel(/Asunto corto/i).fill('Rehabilitación integral de alumbrado público');
  await page.getByLabel(/Estatus de entrega/i).selectOption('EnProceso');
  await page.getByLabel(/¿Tiene acuse\?/i).selectOption('no');
  await page.getByLabel(/Plazo de vencimiento/i).fill(nuevoVencimiento.ddMmYyyy);

  await page.getByRole('button', { name: 'Guardar cambios' }).click();

  // Verificar Modal M-02 y navegación por teclado
  const m02 = page.getByRole('dialog');
  await expect(m02).toBeVisible();
  await expect(m02).toContainText('¿Guardar cambios?');
  await expect(m02).toContainText(
    'Se actualizará la solicitud 00045-A/2026 con los datos modificados.',
  );
  await expect(m02.getByRole('button', { name: 'Cancelar' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(m02.getByRole('button', { name: 'Guardar cambios' })).toBeFocused();

  await m02.getByRole('button', { name: 'Guardar cambios' }).click();

  // Verificar toast de éxito y resumen con datos definitivos del servidor
  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Cambios guardados. Vencimiento y días restantes actualizados.',
  );
  await expect(
    page.getByRole('heading', { name: 'Resumen de solicitud actualizada' }),
  ).toBeVisible();
  await expect(page.getByText('Rehabilitación integral de alumbrado público')).toBeVisible();
  await expect(page.getByText('15 días')).toBeVisible();
  await expect(page.getByText('En proceso')).toBeVisible();

  // Verificar contrato estricto del body PUT (únicamente 11 claves) e If-Match
  expect(seenIfMatch).toEqual(['"etag-inicial-1"']);
  expect(Object.keys(seenBodies[0]).sort()).toEqual([
    'areaReceptoraId',
    'asunto',
    'estatusEntrega',
    'fechaEntrega',
    'fechaVencimiento',
    'horaEntrega',
    'medioEntrega',
    'tieneAcuse',
    'tipoAcuse',
    'tipoEvidencia',
    'tipoSolicitud',
  ]);
  expect(seenBodies[0]).toMatchObject({
    asunto: 'Rehabilitación integral de alumbrado público',
    estatusEntrega: 'EnProceso',
    tieneAcuse: false,
    tipoAcuse: 'NoAplica',
    fechaVencimiento: nuevoVencimiento.iso,
  });

  // Seguir editando usa los nuevos valores base y el nuevo ETag ("etag-servidor-2")
  await page.getByRole('button', { name: 'Seguir editando' }).click();
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Rehabilitación integral de alumbrado público',
  );

  // Sin nuevos cambios detecta equivalencia contra la nueva base
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText('No hay cambios por guardar.');
  expect(seenIfMatch).toHaveLength(1);

  // Un segundo cambio envía el nuevo ETag vigente
  await page.getByLabel(/Asunto corto/i).fill('Segunda actualización consecutiva');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Cambios guardados. Vencimiento y días restantes actualizados.',
  );
  expect(seenIfMatch).toEqual(['"etag-inicial-1"', '"etag-servidor-2"']);
});

test('acceso directo a Editar solicitud desde confirmación exitosa de Task 02 (Nueva solicitud)', async ({
  page,
}) => {
  const vencimiento = getMexicoCityDateOffset(10);
  let requestedEditFolio: string | null = null;

  await setupEditarRoutes(page, {
    onGetEdicion: (route, _count, folioQuery) => {
      requestedEditFolio = folioQuery;
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-from-create"' },
        json: buildMockSolicitud({
          id: 'sol-created-99',
          folio: folioQuery ?? '00099-B/2026',
          asunto: 'Solicitud recién registrada para editar',
          fechaVencimiento: vencimiento.iso,
          diasRestantes: 10,
        }),
      });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await page.getByLabel(/^Folio/i).fill('00099-B/2026');
  const combobox = page.getByRole('combobox', { name: /Área o dependencia receptora/i });
  await combobox.fill('obras');
  await page.getByRole('option', { name: 'Dirección de Obras Públicas' }).click();
  await page.getByLabel(/Asunto corto/i).fill('Solicitud recién registrada para editar');
  await page.getByLabel(/Medio de entrega/i).selectOption('Digital');
  await page.getByLabel(/Estatus de entrega/i).selectOption('EnProceso');
  await page.getByLabel(/Tipo de solicitud/i).selectOption('Informacion');
  await page.getByLabel(/Evidencia que se lleva a cabo/i).selectOption('Carpeta');
  await page.getByLabel(/¿Tiene acuse\?/i).selectOption('si');
  await page.getByLabel(/Tipo de acuse/i).selectOption('Sello');
  await page.getByLabel(/Plazo de vencimiento/i).fill(vencimiento.ddMmYyyy);

  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('heading', { name: 'Resumen de solicitud registrada' })).toBeVisible();
  await page.getByRole('button', { name: 'Editar solicitud' }).click();

  await expect(page).toHaveURL(/\/admin\/solicitudes\/00099-B%2F2026\/editar$/);
  await expect(page.getByRole('heading', { name: 'Editar solicitud', level: 1 })).toBeVisible();
  await expect(page.getByTestId('immutable-folio-chip')).toHaveText('Folio 00099-B/2026');
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Solicitud recién registrada para editar',
  );
  expect(requestedEditFolio).toBe('00099-B/2026');
});

test('falla de carga B-04 (503 o ausencia de ETag) no muestra formulario vacío y permite Reintentar', async ({
  page,
}) => {
  await setupEditarRoutes(page, {
    onGetEdicion: (route, count) => {
      if (count === 1) {
        return route.fulfill({ status: 503, json: { title: 'Unavailable' } });
      }
      if (count === 2) {
        // 200 sin ETag tampoco debe habilitar edición sin versión de concurrencia
        return route.fulfill({ status: 200, json: buildMockSolicitud() });
      }
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-ok-3"' },
        json: buildMockSolicitud(),
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);

  await expect(page.getByRole('alert')).toContainText('No se pudo cargar la solicitud.');
  await expect(page.locator('#solicitud-asunto')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();

  // Intento 2: 200 sin ETag sigue mostrando B-04 y bloqueando edición
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByRole('alert')).toContainText('No se pudo cargar la solicitud.');
  await expect(page.locator('#solicitud-asunto')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();

  // Intento 3: 200 con ETag válido carga el formulario
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeEnabled();
});

test('falla de persistencia B-03 conserva captura, advierte al intentar Cancelar y Reintentar envía el MISMO If-Match original', async ({
  page,
}) => {
  const seenIfMatch: string[] = [];
  const seenPayloads: string[] = [];

  await setupEditarRoutes(page, {
    initialEtag: '"etag-persist-original"',
    onPutSolicitud: (route, count, id) => {
      const req = route.request();
      seenIfMatch.push(req.headers()['if-match']);
      seenPayloads.push(req.postData() ?? '');

      if (count === 1) {
        return route.fulfill({ status: 503, json: { title: 'Error temporal' } });
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-after-retry"' },
        json: {
          id,
          folio: '00045-A/2026',
          ...body,
          diasRestantes: 8,
          estatusEfectivo: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await page.getByLabel(/Asunto corto/i).fill('Asunto con resultado incierto en red');

  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  // Banner B-03 y conservación de datos capturados
  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo.',
  );
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Asunto con resultado incierto en red',
  );
  await expect(page.getByLabel(/Asunto corto/i)).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();

  // Cancelar en estado incierto advierte explícitamente antes de descartar
  await page.getByRole('button', { name: 'Cancelar' }).click();
  const uncertainModal = page.getByRole('dialog');
  await expect(uncertainModal).toContainText('¿Descartar cambios?');
  await expect(uncertainModal).toContainText('incierto');
  await uncertainModal.getByRole('button', { name: 'Seguir editando' }).click();
  await expect(uncertainModal).toHaveCount(0);

  // Reintentar envía exactamente el mismo payload y el MISMO If-Match original
  await page.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Cambios guardados. Vencimiento y días restantes actualizados.',
  );
  expect(seenIfMatch).toEqual(['"etag-persist-original"', '"etag-persist-original"']);
  expect(seenPayloads[0]).toBe(seenPayloads[1]);
});

test('conflicto de concurrencia 409 -> Modal M-05 -> Cerrar conserva captura y exige recarga -> Recargar datos actualiza ETag y permite guardar', async ({
  page,
}) => {
  const seenIfMatch: string[] = [];
  const vencimientoVigente = getMexicoCityDateOffset(14);

  await setupEditarRoutes(page, {
    initialEtag: '"etag-old-v1"',
    onGetEdicion: (route, count) => {
      if (count === 1) {
        return route.fulfill({
          status: 200,
          headers: { ETag: '"etag-old-v1"' },
          json: buildMockSolicitud({ asunto: 'Asunto inicial v1' }),
        });
      }
      if (count === 2) {
        // Simular fallo transitorio en la primera recarga para comprobar que NO sobrescribe cambios locales
        return route.fulfill({ status: 503 });
      }
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-fresh-v2"' },
        json: buildMockSolicitud({
          asunto: 'Asunto modificado por otro administrador',
          fechaVencimiento: vencimientoVigente.iso,
          diasRestantes: 14,
        }),
      });
    },
    onPutSolicitud: (route, count, id) => {
      const req = route.request();
      seenIfMatch.push(req.headers()['if-match']);
      if (count === 1) {
        return route.fulfill({
          status: 409,
          json: { code: 'Solicitud.ConcurrencyConflict' },
        });
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-final-v3"' },
        json: {
          id,
          folio: '00045-A/2026',
          ...body,
          diasRestantes: 14,
          estatusEfectivo: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue('Asunto inicial v1');

  await page.getByLabel(/Asunto corto/i).fill('Mi captura local antes del conflicto');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  // Modal M-05
  const m05 = page.getByRole('dialog');
  await expect(m05).toBeVisible();
  await expect(m05).toContainText('La solicitud cambió');
  await expect(m05).toContainText(
    'Otro proceso modificó esta solicitud. Recarga para ver los datos vigentes; tus cambios no se guardaron.',
  );
  await expect(m05.getByRole('button', { name: 'Cerrar' })).toBeFocused();

  // Cerrar conserva captura del usuario, deshabilita Guardar cambios y muestra explicación
  await m05.getByRole('button', { name: 'Cerrar' }).click();
  await expect(m05).toHaveCount(0);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Mi captura local antes del conflicto',
  );
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();
  await expect(page.getByRole('alert')).toContainText('Recarga los datos para continuar');

  // Si la recarga falla (count === 2 -> 503), NO reemplaza la captura local
  await page.getByRole('button', { name: 'Recargar datos' }).click();
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Mi captura local antes del conflicto',
  );
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();

  // Recargar datos exitosa (count === 3) carga datos vigentes + nuevo ETag y rehabilita Guardar cambios
  await page.getByRole('button', { name: 'Recargar datos' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Datos actualizados a la versión vigente',
  );
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Asunto modificado por otro administrador',
  );
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeEnabled();

  // Editar sobre la nueva base y guardar envía el nuevo If-Match ("etag-fresh-v2")
  await page.getByLabel(/Asunto corto/i).fill('Asunto conciliado sobre versión vigente');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Cambios guardados. Vencimiento y días restantes actualizados.',
  );
  expect(seenIfMatch).toEqual(['"etag-old-v1"', '"etag-fresh-v2"']);
});

test('registro inexistente (404 en GET y 404 en PUT) abre Modal M-06 y navega con Volver a la lista', async ({
  page,
}) => {
  // Caso A: 404 en GET inicial
  await setupEditarRoutes(page, {
    onGetEdicion: (route) => route.fulfill({ status: 404, json: { title: 'Not Found' } }),
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  const m06Get = page.getByRole('dialog');
  await expect(m06Get).toBeVisible();
  await expect(m06Get).toContainText('Solicitud no disponible');
  await expect(m06Get).toContainText('La solicitud 00045-A/2026 ya no existe.');
  await expect(m06Get.getByRole('button', { name: 'Volver a la lista' })).toBeFocused();
  await m06Get.getByRole('button', { name: 'Volver a la lista' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);

  // Caso B: GET exitoso pero 404 al enviar PUT
  await page.unrouteAll();
  await setupEditarRoutes(page, {
    onPutSolicitud: (route) => route.fulfill({ status: 404, json: { title: 'Deleted' } }),
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await page.getByLabel(/Asunto corto/i).fill('Cambio sobre solicitud recién eliminada');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  const m06Put = page.getByRole('dialog');
  await expect(m06Put).toBeVisible();
  await expect(m06Put).toContainText('Solicitud no disponible');
  await expect(m06Put).toContainText('La solicitud 00045-A/2026 ya no existe.');
  await m06Put.getByRole('button', { name: 'Volver a la lista' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
});

test('cancelación M-03 (Seguir editando / Escape / Descartar), protección Dashboard en AdminShell y 400 ValidationProblemDetails', async ({
  page,
}) => {
  let putCount = 0;
  await setupEditarRoutes(page, {
    onPutSolicitud: (route) => {
      putCount += 1;
      if (putCount === 1) {
        return route.fulfill({
          status: 400,
          json: {
            errors: {
              asunto: ['El asunto contiene caracteres no permitidos por política.'],
            },
          },
        });
      }
      const body = route.request().postDataJSON();
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-post-400"' },
        json: {
          ...buildMockSolicitud(),
          ...body,
          diasRestantes: 8,
          estatusEfectivo: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );

  // Pulsar Cancelar abre SIEMPRE M-03 incluso sin cambios; Escape y Seguir editando cierran el modal
  await page.getByRole('button', { name: 'Cancelar' }).click();
  const m03 = page.getByRole('dialog');
  await expect(m03).toContainText('¿Descartar cambios?');
  await expect(m03).toContainText('Los datos capturados no se guardarán.');
  await expect(m03.getByRole('button', { name: 'Seguir editando' })).toBeFocused();

  // Focus trap con Tab / Shift+Tab en M-03
  await page.keyboard.press('Tab');
  await expect(m03.getByRole('button', { name: 'Descartar' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(m03.getByRole('button', { name: 'Seguir editando' })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(m03).toHaveCount(0);

  // Validación 400 del servidor enfoca el campo marcado y conserva ETag para reintentar corregido
  await page.getByLabel(/Asunto corto/i).fill('Texto rechazado por backend');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  await expect(page.locator('#error-asunto')).toHaveText(
    'El asunto contiene caracteres no permitidos por política.',
  );
  await expect(page.locator('#solicitud-asunto')).toBeFocused();

  // Protección de salida por Dashboard en AdminShell cuando hay cambios pendientes
  await page.getByRole('button', { name: 'Dashboard' }).click();
  const m03FromNav = page.getByRole('dialog');
  await expect(m03FromNav).toContainText('¿Descartar cambios?');
  await m03FromNav.getByRole('button', { name: 'Seguir editando' }).click();
  await expect(m03FromNav).toHaveCount(0);

  // Descartar desde Cancelar navega a /admin/dashboard
  await page.getByRole('button', { name: 'Cancelar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
});

test('412 abre M-05 igual que 409, expiración 401 invalida sesión y barra fija móvil de 72px en V06', async ({
  page,
}) => {
  let sessionValid = true;
  let putAttempt = 0;

  await page.route('https://localhost:5173/api/**', async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname;

    if (path === '/api/auth/me') {
      return sessionValid
        ? route.fulfill({
            json: { userId: 'admin-1', userName: 'Ana Administradora', roles: ['Administrador'] },
          })
        : route.fulfill({ status: 401 });
    }
    if (path === '/api/auth/csrf') {
      return route.fulfill({ json: { requestToken: 'csrf-412-401' } });
    }
    if (path === '/api/solicitudes/catalogos') {
      return route.fulfill({ json: MOCK_CATALOGOS });
    }
    if (path === '/api/solicitudes/edicion') {
      return route.fulfill({
        status: 200,
        headers: { ETag: '"etag-412-test"' },
        json: buildMockSolicitud({ folio: url.searchParams.get('folio') ?? '00045-A/2026' }),
      });
    }
    if (path.startsWith('/api/solicitudes/') && req.method() === 'PUT') {
      putAttempt += 1;
      if (putAttempt === 1) {
        return route.fulfill({ status: 412, json: { title: 'Precondition Failed' } });
      }
      sessionValid = false;
      return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
    }
    return route.abort();
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);

  const mobileBarHeight = await page
    .locator('.solicitud-actions-bar')
    .evaluate((el) => el.getBoundingClientRect().height);
  expect(mobileBarHeight).toBe(72);

  // 412 abre M-05
  await page.getByLabel(/Asunto corto/i).fill('Cambio para probar 412 y luego 401');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();

  const m05 = page.getByRole('dialog');
  await expect(m05).toContainText('La solicitud cambió');
  await m05.getByRole('button', { name: 'Recargar datos' }).click();
  await expect(page.getByTestId('solicitud-toast')).toContainText(
    'Datos actualizados a la versión vigente',
  );

  // 401 en PUT invalida sesión y redirige a /admin/login
  await page.getByLabel(/Asunto corto/i).fill('Cambio cuando la sesión expiró');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);
});

test('reingreso SPA con caché previa muestra skeleton durante refetch y activa B-04 (503) o M-06 (404) sin hidratar datos obsoletos', async ({
  page,
}) => {
  let releaseReentry503!: () => void;
  const reentry503Gate = new Promise<void>((resolve) => {
    releaseReentry503 = resolve;
  });

  await setupEditarRoutes(page, {
    onGetEdicion: async (route, count) => {
      if (count === 1) {
        return route.fulfill({
          status: 200,
          headers: { ETag: '"etag-cache-v1"' },
          json: buildMockSolicitud({ asunto: 'Asunto inicial en caché v1' }),
        });
      }
      if (count === 2) {
        // Reingreso SPA 1: retener para comprobar skeleton y luego responder 503
        await reentry503Gate;
        return route.fulfill({ status: 503, json: { title: 'Service Unavailable' } });
      }
      if (count === 3) {
        // Reintentar tras B-04 carga nueva versión vigente
        return route.fulfill({
          status: 200,
          headers: { ETag: '"etag-cache-v3"' },
          json: buildMockSolicitud({ asunto: 'Asunto vigente tras reintento v3' }),
        });
      }
      // Reingreso SPA 2 (count >= 4): el registro fue eliminado -> 404
      return route.fulfill({ status: 404, json: { title: 'Not Found' } });
    },
  });

  // 1. Primera entrada y PUT exitoso que puebla queryClient.setQueryData(['solicitudes', 'edicion', folio])
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue('Asunto inicial en caché v1');

  await page.getByLabel(/Asunto corto/i).fill('Asunto guardado en caché tras PUT');
  await page.getByRole('button', { name: 'Guardar cambios' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(
    page.getByRole('heading', { name: 'Resumen de solicitud actualizada' }),
  ).toBeVisible();

  // Salir por SPA a /admin/dashboard (conservando la instancia de QueryClient en memoria)
  await page.getByRole('button', { name: 'Volver al panel' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
  await expect(page.getByRole('heading', { name: 'Panel de Administrador' })).toBeVisible();

  // 2. Reingreso SPA al mismo folio: mientras refetch está en vuelo muestra skeleton y NO datos obsoletos
  await page.goBack();
  await expect(page).toHaveURL(/\/admin\/solicitudes\/00045-A%2F2026\/editar$/);
  await expect(page.getByTestId('solicitud-skeleton-control')).toHaveCount(12);
  await expect(page.locator('#solicitud-asunto')).toHaveCount(0);

  // Cuando el refetch falla con 503, muestra B-04 y jamás hidrata los datos en caché previos
  releaseReentry503();
  await expect(page.getByRole('alert')).toContainText('No se pudo cargar la solicitud.');
  await expect(page.locator('#solicitud-asunto')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();

  // Reintentar recupera versión fresca v3
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue('Asunto vigente tras reintento v3');

  // 3. Salir nuevamente por SPA a /admin/dashboard y reingresar cuando ahora devuelve 404 -> M-06
  await page.getByRole('button', { name: 'Cancelar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
  await expect(page.getByRole('heading', { name: 'Panel de Administrador' })).toBeVisible();

  await page.goBack();
  const m06 = page.getByRole('dialog');
  await expect(m06).toBeVisible();
  await expect(m06).toContainText('Solicitud no disponible');
  await expect(m06).toContainText('La solicitud 00045-A/2026 ya no existe.');
  await expect(page.locator('#solicitud-asunto')).toHaveCount(0);

  await m06.getByRole('button', { name: 'Volver a la lista' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
});

test('viewport móvil estrecho (360x800): los 3 botones de V06 caben en una sola línea dentro de 72px sin desbordamiento horizontal', async ({
  page,
}) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await setupEditarRoutes(page);
  await page.goto(`/admin/solicitudes/${encodeURIComponent('00045-A/2026')}/editar`);
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue(
    'Construcción de guarniciones y banquetas',
  );

  const layoutMetrics = await page.locator('.solicitud-actions-bar').evaluate((bar) => {
    const doc = bar.ownerDocument;
    const buttons = Array.from(bar.querySelectorAll('.solicitud-actions-buttons .solicitud-btn'));
    const barRect = bar.getBoundingClientRect();
    const rects = buttons.map((btn) => {
      const htmlBtn = btn as typeof bar;
      const r = htmlBtn.getBoundingClientRect();
      return {
        label: htmlBtn.textContent?.trim() ?? '',
        top: Math.round(r.top),
        bottom: Math.round(r.bottom),
        left: r.left,
        right: r.right,
        height: Math.round(r.height),
        clientWidth: htmlBtn.clientWidth,
        scrollWidth: htmlBtn.scrollWidth,
      };
    });

    return {
      docScrollWidth: doc.documentElement.scrollWidth,
      barHeight: Math.round(barRect.height),
      barLeft: barRect.left,
      barRight: barRect.right,
      rects,
    };
  });

  expect(layoutMetrics.docScrollWidth).toBeLessThanOrEqual(360);
  expect(layoutMetrics.barHeight).toBe(72);
  expect(layoutMetrics.rects).toHaveLength(3);

  // Los 3 botones están alineados en la misma fila horizontal, con 40px de alto y sin truncar/partir texto en 2 líneas
  const referenceTop = layoutMetrics.rects[0].top;
  for (const btnMetric of layoutMetrics.rects) {
    expect(btnMetric.top).toBe(referenceTop);
    expect(btnMetric.height).toBe(40);
    expect(btnMetric.left).toBeGreaterThanOrEqual(0);
    expect(btnMetric.right).toBeLessThanOrEqual(360);
    expect(btnMetric.scrollWidth).toBeLessThanOrEqual(btnMetric.clientWidth + 1);
  }
});
