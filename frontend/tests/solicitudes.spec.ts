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

async function setupAdminSessionRoutes(
  page: Page,
  options: {
    roles?: string[] | null;
    onCatalogos?: (route: Route, count: number) => Promise<void> | void;
    onCreateSolicitud?: (route: Route, count: number) => Promise<void> | void;
  } = {},
) {
  const { roles = ['Administrador'], onCatalogos, onCreateSolicitud } = options;
  let csrfToken = randomUUID();
  let catalogosCount = 0;
  let createCount = 0;

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
      if (onCatalogos) {
        return onCatalogos(route, catalogosCount);
      }
      return route.fulfill({ json: MOCK_CATALOGOS });
    }

    if (path === '/api/solicitudes' && request.method() === 'POST') {
      const headerCsrf = request.headers()['x-csrf-token'];
      if (!headerCsrf || headerCsrf !== csrfToken) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }
      createCount += 1;
      if (onCreateSolicitud) {
        return onCreateSolicitud(route, createCount);
      }
      const body = request.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: randomUUID(),
          folio: body.generarFolioAutomatico ? 'SOL-2026-0001' : body.folio,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    }

    return route.abort();
  });
}

async function fillValidForm(page: Page, folio = '000459826') {
  const vencimiento = getMexicoCityDateOffset(10);

  await page.getByLabel(/^Folio/i).fill(folio);

  const areaCombobox = page.getByRole('combobox', { name: /Área o dependencia receptora/i });
  await areaCombobox.fill('obras');
  await page.getByRole('option', { name: 'Dirección de Obras Públicas' }).click();

  await page.getByLabel(/Asunto corto/i).fill('Solicitud de información presupuestal');
  await page.getByLabel(/Medio de entrega/i).selectOption('Digital');
  await page.getByLabel(/Estatus de entrega/i).selectOption('EnProceso');
  await page.getByLabel(/Tipo de solicitud/i).selectOption('Informacion');
  await page.getByLabel(/Evidencia que se lleva a cabo/i).selectOption('Carpeta');
  await page.getByLabel(/¿Tiene acuse\?/i).selectOption('si');
  await page.getByLabel(/Tipo de acuse/i).selectOption('Sello');
  await page.getByLabel(/Plazo de vencimiento/i).fill(vencimiento.ddMmYyyy);

  return vencimiento;
}

test('protección de ruta: anónimo redirige, Consulta ve Acceso denegado sin cargar catálogos y Admin navega desde dashboard', async ({
  page,
}) => {
  await setupAdminSessionRoutes(page, { roles: null });
  await page.goto('/admin/solicitudes/nueva');
  await expect(page).toHaveURL(/\/admin\/login$/);

  await page.unrouteAll();
  let catalogosCalledByConsulta = false;
  await setupAdminSessionRoutes(page, {
    roles: ['Consulta'],
    onCatalogos: (route) => {
      catalogosCalledByConsulta = true;
      return route.fulfill({ json: MOCK_CATALOGOS });
    },
  });
  await page.goto('/admin/solicitudes/nueva');
  await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Ir a mi panel' })).toBeVisible();
  expect(catalogosCalledByConsulta).toBe(false);

  await page.unrouteAll();
  await setupAdminSessionRoutes(page, { roles: ['Administrador'] });
  await page.goto('/admin/dashboard');
  await expect(page.getByRole('heading', { name: 'Panel de Administrador' })).toBeVisible();
  await page.getByRole('link', { name: 'Nueva solicitud' }).click();

  await expect(page).toHaveURL(/\/admin\/solicitudes\/nueva$/);
  await expect(page.getByRole('heading', { name: 'Nueva solicitud', level: 1 })).toBeVisible();
  await expect(page.getByText('Gestión de Solicitudes')).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Breadcrumb' })).toContainText(
    'Solicitudes›Nueva solicitud',
  );
  await expect(page.getByText('Ana Administradora')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Solicitudes' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  for (const disabledLabel of [
    'Obras',
    'Contratos',
    'Avances',
    'Reportes',
    'Documentos',
    'Usuarios',
    'Configuración',
  ]) {
    await expect(page.getByRole('button', { name: disabledLabel })).toBeDisabled();
  }
});

test('carga y error de catálogos con Reintentar y exclusión de estatus Vencida', async ({
  page,
}) => {
  await setupAdminSessionRoutes(page, {
    onCatalogos: (route, count) => {
      if (count === 1) {
        return route.fulfill({ status: 503 });
      }
      return route.fulfill({ json: MOCK_CATALOGOS });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await expect(page.getByRole('alert')).toContainText('No fue posible cargar los catálogos');
  await page.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByLabel(/Medio de entrega/i)).toBeEnabled();
  const estatusOptions = await page
    .getByLabel(/Estatus de entrega/i)
    .locator('option')
    .allTextContents();
  expect(estatusOptions).toContain('En proceso');
  expect(estatusOptions).toContain('Entregada');
  expect(estatusOptions).not.toContain('Vencida');
});

test('modalidades de folio: conserva ceros iniciales y alterna automático/manual sin llamar API', async ({
  page,
}) => {
  let postCalls = 0;
  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route) => {
      postCalls += 1;
      return route.fulfill({ status: 201, json: {} });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  const folioInput = page.getByLabel(/^Folio/i);
  await folioInput.fill('000125');
  await expect(folioInput).toHaveValue('000125');

  await page.getByRole('button', { name: 'Generar folio automático' }).click();
  await expect(folioInput).toBeDisabled();
  await expect(folioInput).toHaveValue('Se asignará al guardar');
  await expect(page.getByText('# Folio: se asignará al guardar')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Capturar folio manual' })).toBeVisible();
  expect(postCalls).toBe(0);

  await page.getByRole('button', { name: 'Capturar folio manual' }).click();
  await expect(folioInput).toBeEnabled();
  await expect(folioInput).toHaveValue('000125');
  await expect(page.getByText('# Folio: 000125')).toBeVisible();
  expect(postCalls).toBe(0);
});

test('reglas de acuse, contador de asunto, combobox por teclado, fechas/horas inválidas y días restantes', async ({
  page,
}) => {
  await setupAdminSessionRoutes(page);
  await page.goto('/admin/solicitudes/nueva');

  // Regla de acuse
  const tieneAcuse = page.getByLabel(/¿Tiene acuse\?/i);
  const tipoAcuse = page.getByLabel(/Tipo de acuse/i);
  await tieneAcuse.selectOption('no');
  await expect(tipoAcuse).toBeDisabled();
  await expect(tipoAcuse).toHaveValue('NoAplica');

  await tieneAcuse.selectOption('si');
  await expect(tipoAcuse).toBeEnabled();
  await expect(tipoAcuse).toHaveValue('');
  const tiposAcuseOptions = await tipoAcuse.locator('option').allTextContents();
  expect(tiposAcuseOptions).not.toContain('No aplica');

  // Contador de asunto sin truncamiento
  const asunto = page.getByLabel(/Asunto corto/i);
  const counter = page.locator('#asunto-counter');
  await asunto.fill('a'.repeat(50));
  await expect(counter).toHaveText('50/100');
  await expect(counter).not.toHaveClass(/is-warning|is-error/);

  await asunto.fill('a'.repeat(95));
  await expect(counter).toHaveText('95/100');
  await expect(counter).toHaveClass(/is-warning/);

  await asunto.fill('a'.repeat(105));
  await expect(asunto).toHaveValue('a'.repeat(105));
  await expect(counter).toHaveText('105/100');
  await expect(counter).toHaveClass(/is-error/);
  await expect(page.locator('#error-asunto')).toContainText('no puede exceder 100 caracteres');

  // Combobox con búsqueda y teclado
  const combobox = page.getByRole('combobox', { name: /Área o dependencia receptora/i });
  await combobox.click();
  await combobox.fill('finanzas');
  await combobox.press('ArrowDown');
  await combobox.press('Enter');
  await expect(combobox).toHaveValue('Secretaría de Finanzas');

  // Fechas y horas inválidas conservan el valor ingresado y muestran error
  const fechaEntrega = page.getByLabel(/Fecha de entrega/i);
  const horaEntrega = page.getByLabel(/Hora de entrega/i);
  const vencimiento = page.getByLabel(/Plazo de vencimiento/i);

  const todayMx = getMexicoCityDateOffset(0);
  await expect(fechaEntrega).toHaveValue(todayMx.ddMmYyyy);
  await expect(horaEntrega).toHaveValue(/^([01]\d|2[0-3]):[0-5]\d$/);

  await fechaEntrega.fill('31/02/2026');
  await fechaEntrega.blur();
  await expect(fechaEntrega).toHaveValue('31/02/2026');
  await expect(page.locator('#error-fecha-entrega')).toContainText('fecha válida');

  await horaEntrega.fill('25:00');
  await horaEntrega.blur();
  await expect(horaEntrega).toHaveValue('25:00');
  await expect(page.locator('#error-hora-entrega')).toContainText('hora válida');

  // Días restantes y vencimiento anterior a entrega
  await fechaEntrega.fill(todayMx.ddMmYyyy);
  await vencimiento.fill('31/02/2026');
  await vencimiento.blur();
  const diasControl = page.getByRole('status').filter({ hasText: /Días restantes:/ });
  await expect(diasControl).toHaveAttribute('data-tone', 'empty');
  await expect(diasControl).toContainText('Días restantes: —');

  const yesterdayMx = getMexicoCityDateOffset(-1);
  await vencimiento.fill(yesterdayMx.ddMmYyyy);
  await vencimiento.blur();
  await expect(page.locator('#error-fecha-vencimiento')).toContainText(
    'no puede ser anterior a la fecha de entrega',
  );

  await vencimiento.fill(todayMx.ddMmYyyy);
  await expect(diasControl).toHaveAttribute('data-tone', 'urgent');
  await expect(diasControl).toContainText('Días restantes: 0 días');

  await vencimiento.fill(getMexicoCityDateOffset(3).ddMmYyyy);
  await expect(diasControl).toHaveAttribute('data-tone', 'warning');
  await expect(diasControl).toContainText('Días restantes: 3 días');

  await vencimiento.fill(getMexicoCityDateOffset(10).ddMmYyyy);
  await expect(diasControl).toHaveAttribute('data-tone', 'normal');
  await expect(diasControl).toContainText('Días restantes: 10 días');
});

test('guardar inválido muestra Revisa los campos marcados (N), enfoca el primero y no llama al API', async ({
  page,
}) => {
  let postCalls = 0;
  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route) => {
      postCalls += 1;
      return route.fulfill({ status: 201, json: {} });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await page.getByLabel(/Asunto corto/i).fill('Asunto parcial que debe conservarse');

  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(page.getByRole('alert')).toContainText('Revisa los campos marcados (8)');
  await expect(page.locator('#solicitud-folio')).toBeFocused();
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue('Asunto parcial que debe conservarse');
  expect(postCalls).toBe(0);
});

test('guardar válido abre M-01 con foco en Cancelar, envía CSRF e Idempotency-Key y permite Registrar otra solicitud', async ({
  page,
}) => {
  let capturedHeaders: Record<string, string> = {};
  let capturedBody: Record<string, unknown> = {};

  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route) => {
      const req = route.request();
      capturedHeaders = req.headers();
      capturedBody = req.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: 'sol-100',
          folio: '000459826',
          fechaVencimiento: capturedBody.fechaVencimiento,
          diasRestantes: 10,
          estatus: 'EnProceso',
        },
      });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  const todayMx = getMexicoCityDateOffset(0);
  const vencimiento = await fillValidForm(page, '000459826');

  await page.getByRole('button', { name: 'Guardar' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText('¿Registrar solicitud?');
  await expect(dialog).toContainText('000459826');
  await expect(dialog.getByRole('button', { name: 'Cancelar' })).toBeFocused();

  await dialog.getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('status')).toContainText(
    'Solicitud 000459826 registrada correctamente',
  );
  await expect(page.getByRole('heading', { name: 'Resumen de solicitud registrada' })).toBeVisible();
  expect(capturedHeaders['x-csrf-token']).toBeTruthy();
  expect(capturedHeaders['idempotency-key']).toMatch(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  );
  expect(capturedBody).toMatchObject({
    generarFolioAutomatico: false,
    folio: '000459826',
    fechaEntrega: todayMx.iso,
    areaReceptoraId: 'direccion-obras-publicas',
    asunto: 'Solicitud de información presupuestal',
    medioEntrega: 'Digital',
    estatusEntrega: 'EnProceso',
    tipoSolicitud: 'Informacion',
    tipoEvidencia: 'Carpeta',
    tieneAcuse: true,
    tipoAcuse: 'Sello',
    fechaVencimiento: vencimiento.iso,
  });

  await page.getByRole('button', { name: 'Registrar otra solicitud' }).click();
  await expect(page.getByLabel(/^Folio/i)).toHaveValue('');
  await expect(page.getByLabel(/Asunto corto/i)).toHaveValue('');
});

test('folio duplicado (409) muestra mensaje junto al campo y al corregir genera nueva Idempotency-Key', async ({
  page,
}) => {
  const idempotencyKeys: string[] = [];

  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route, count) => {
      const req = route.request();
      idempotencyKeys.push(req.headers()['idempotency-key']);
      if (count === 1) {
        return route.fulfill({
          status: 409,
          json: { code: 'Solicitud.FolioDuplicado' },
        });
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: 'sol-101',
          folio: body.folio,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'DUP-001');

  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.locator('#error-folio')).toHaveText(
    'El folio ingresado ya está registrado. Introduce uno diferente',
  );
  await expect(page.locator('#solicitud-folio')).toBeEnabled();
  await expect(page.locator('#solicitud-folio')).toBeFocused();

  await page.getByLabel(/^Folio/i).fill('UNICO-002');
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('status')).toContainText(
    'Solicitud UNICO-002 registrada correctamente',
  );
  expect(idempotencyKeys).toHaveLength(2);
  expect(idempotencyKeys[0]).not.toBe(idempotencyKeys[1]);
});

test('falla de persistencia (503) conserva datos, bloquea edición, maneja Cancelar incierto y reintenta con la misma Idempotency-Key', async ({
  page,
}) => {
  const seenKeys: string[] = [];
  const seenPayloads: string[] = [];

  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route, count) => {
      const req = route.request();
      seenKeys.push(req.headers()['idempotency-key']);
      seenPayloads.push(req.postData() ?? '');
      if (count === 1) {
        return route.fulfill({ status: 503, json: { title: 'Servicio no disponible' } });
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 200,
        json: {
          id: 'sol-102',
          folio: body.folio,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'RETRY-009');

  // Probar modal M-03 normal antes de guardar
  await page.getByRole('button', { name: 'Cancelar' }).click();
  const discardDialog = page.getByRole('dialog');
  await expect(discardDialog).toContainText('¿Descartar cambios?');
  await expect(discardDialog).toContainText('Los datos capturados no se guardarán.');
  await expect(discardDialog.getByRole('button', { name: 'Seguir editando' })).toBeFocused();
  await discardDialog.getByRole('button', { name: 'Seguir editando' }).click();
  await expect(discardDialog).toHaveCount(0);

  // Enviar y recibir 503
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );
  await expect(page.getByLabel(/^Folio/i)).toHaveValue('RETRY-009');
  await expect(page.getByLabel(/^Folio/i)).toBeDisabled();
  await expect(page.getByLabel(/Asunto corto/i)).toBeDisabled();

  // Cancelar durante resultado incierto NO afirma que no se guardó y ofrece Seguir aquí / Reintentar / Salir al panel
  await page.getByRole('button', { name: 'Cancelar' }).click();
  const uncertainDialog = page.getByRole('dialog');
  await expect(uncertainDialog).toContainText('resultado de la solicitud anterior es incierto');
  await expect(uncertainDialog.getByRole('button', { name: 'Seguir aquí' })).toBeFocused();
  await expect(uncertainDialog.getByRole('button', { name: 'Reintentar' })).toBeVisible();
  await expect(uncertainDialog.getByRole('button', { name: 'Salir al panel' })).toBeVisible();
  await uncertainDialog.getByRole('button', { name: 'Seguir aquí' }).click();

  // Reintentar desde Banner B-03 reutiliza exactamente la misma Idempotency-Key y payload
  await page.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByRole('status')).toContainText(
    'Solicitud RETRY-009 registrada correctamente',
  );
  expect(seenKeys).toHaveLength(2);
  expect(seenKeys[0]).toBe(seenKeys[1]);
  expect(seenPayloads[0]).toBe(seenPayloads[1]);
});

test('folio automático, fallo de red con reintento desde modal incierto, y navegación Dashboard con descarte', async ({
  page,
}) => {
  const seenKeys: string[] = [];
  const seenBodies: Array<Record<string, unknown>> = [];

  await setupAdminSessionRoutes(page, {
    onCreateSolicitud: (route, count) => {
      const req = route.request();
      seenKeys.push(req.headers()['idempotency-key']);
      seenBodies.push(req.postDataJSON());
      if (count === 1) {
        return route.abort('connectionfailed');
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: 'sol-auto-1',
          folio: 'AUTO-2026-0042',
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    },
  });

  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'BORRADOR');
  await page.getByRole('button', { name: 'Generar folio automático' }).click();
  await page.getByLabel(/¿Tiene acuse\?/i).selectOption('no');

  await page.getByRole('button', { name: 'Guardar' }).click();
  const confirmDialog = page.getByRole('dialog');
  await expect(confirmDialog).toContainText('Se asignará automáticamente al guardar');
  await confirmDialog.getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );

  // Al pulsar Dashboard en menú lateral durante envío incierto, abre modal incierto y permite Reintentar
  await page.getByRole('button', { name: 'Dashboard' }).click();
  const uncertainDialog = page.getByRole('dialog');
  await expect(uncertainDialog).toContainText('resultado de la solicitud anterior es incierto');
  await uncertainDialog.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page.getByRole('status')).toContainText(
    'Solicitud AUTO-2026-0042 registrada correctamente',
  );
  expect(seenKeys).toHaveLength(2);
  expect(seenKeys[0]).toBe(seenKeys[1]);
  expect(seenBodies[0]).toMatchObject({
    generarFolioAutomatico: true,
    folio: null,
    tieneAcuse: false,
    tipoAcuse: 'NoAplica',
  });

  // Volver a capturar y probar Descartar hacia /admin/dashboard
  await page.getByRole('button', { name: 'Registrar otra solicitud' }).click();
  await page.getByLabel(/^Folio/i).fill('DESCARTAR-1');
  await page.getByRole('button', { name: 'Dashboard' }).click();
  const discardDialog = page.getByRole('dialog');
  await expect(discardDialog).toContainText('¿Descartar cambios?');
  await discardDialog.getByRole('button', { name: 'Descartar' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);
});

test('responsive móvil (<= 768px): drawer lateral accesible con focus trap/inert y barra inferior fija', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setupAdminSessionRoutes(page);
  await page.goto('/admin/solicitudes/nueva');

  const openDrawerBtn = page.getByRole('button', { name: 'Abrir menú lateral' });
  const closeDrawerBtn = page.getByRole('button', { name: 'Cerrar menú lateral' });
  const mainWrapper = page.locator('.admin-main-wrapper');

  await openDrawerBtn.click();
  await expect(closeDrawerBtn).toBeVisible();
  await expect(closeDrawerBtn).toBeFocused();
  await expect(mainWrapper).toHaveAttribute('inert', '');

  // Tab / Shift+Tab se mantienen atrapados dentro del drawer móvil
  await page.keyboard.press('Shift+Tab');
  await expect(page.getByRole('button', { name: 'Cerrar sesión' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(closeDrawerBtn).toBeFocused();

  // Cerrar con Escape restaura el foco al disparador y quita inert
  await page.keyboard.press('Escape');
  await expect(mainWrapper).not.toHaveAttribute('inert', '');
  await expect(openDrawerBtn).toBeFocused();

  // Cerrar por botón y por backdrop también restaura foco
  await openDrawerBtn.click();
  await expect(closeDrawerBtn).toBeFocused();
  await closeDrawerBtn.click();
  await expect(openDrawerBtn).toBeFocused();

  await openDrawerBtn.click();
  await expect(closeDrawerBtn).toBeFocused();
  await page.locator('.admin-backdrop').click({ position: { x: 340, y: 200 } });
  await expect(openDrawerBtn).toBeFocused();

  const actionsBarHeight = await page
    .locator('.solicitud-actions-bar')
    .evaluate((el) => el.getBoundingClientRect().height);
  expect(actionsBarHeight).toBe(72);
});

test('layout desktop/tablet, focus trap en M-01, y 400 con ValidationProblemDetails + refresco CSRF', async ({
  page,
}) => {
  let csrfCalls = 0;
  let createCalls = 0;
  const keys: string[] = [];

  await page.route('https://localhost:5173/api/**', async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;

    if (path === '/api/auth/me') {
      return route.fulfill({
        json: { userId: 'admin-1', userName: 'Ana Administradora', roles: ['Administrador'] },
      });
    }
    if (path === '/api/auth/csrf') {
      csrfCalls += 1;
      return route.fulfill({ json: { requestToken: `csrf-${csrfCalls}` } });
    }
    if (path === '/api/solicitudes/catalogos') {
      return route.fulfill({ json: MOCK_CATALOGOS });
    }
    if (path === '/api/solicitudes' && req.method() === 'POST') {
      createCalls += 1;
      keys.push(req.headers()['idempotency-key']);
      if (createCalls === 1) {
        return route.fulfill({
          status: 400,
          json: {
            errors: {
              asunto: ['El asunto contiene términos no válidos.'],
            },
          },
        });
      }
      const body = req.postDataJSON();
      return route.fulfill({
        status: 201,
        json: {
          id: 'sol-400-ok',
          folio: body.folio,
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        },
      });
    }
    return route.abort();
  });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/admin/solicitudes/nueva');

  const sidebarDesktop = await page
    .locator('.admin-sidebar')
    .evaluate((el) => el.getBoundingClientRect().width);
  const topbarDesktop = await page
    .locator('.admin-topbar')
    .evaluate((el) => el.getBoundingClientRect().height);
  const paddingDesktop = await page
    .locator('.admin-content')
    .evaluate((el) => el.ownerDocument.defaultView?.getComputedStyle(el).paddingTop);
  expect(sidebarDesktop).toBe(260);
  expect(topbarDesktop).toBe(64);
  expect(paddingDesktop).toBe('32px');

  await page.setViewportSize({ width: 1280, height: 800 });
  const padding1280 = await page
    .locator('.admin-content')
    .evaluate((el) => el.ownerDocument.defaultView?.getComputedStyle(el).paddingTop);
  expect(padding1280).toBe('24px');

  await page.setViewportSize({ width: 1024, height: 768 });
  await expect
    .poll(() => page.locator('.admin-sidebar').evaluate((el) => Math.round(el.getBoundingClientRect().width)))
    .toBe(72);

  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(page.locator('.solicitud-footer-note')).toHaveText('* Campos obligatorios');
  await fillValidForm(page, 'VAL-400-1');

  await page.getByRole('button', { name: 'Guardar' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('button', { name: 'Cancelar' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Registrar' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Cancelar' })).toBeFocused();

  await dialog.getByRole('button', { name: 'Registrar' }).click();

  await expect(page.locator('#error-asunto')).toHaveText(
    'El asunto contiene términos no válidos.',
  );
  await expect(page.locator('#solicitud-asunto')).toBeFocused();

  await page.getByLabel(/Asunto corto/i).fill('Asunto corregido tras validación');
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('status')).toContainText(
    'Solicitud VAL-400-1 registrada correctamente',
  );
  expect(csrfCalls).toBe(2);
  expect(keys).toHaveLength(2);
  expect(keys[0]).not.toBe(keys[1]);
});

test('manejo de 409 Solicitud.IdempotenciaConflicto y expiración de sesión 401 en catálogos o envío', async ({
  page,
}) => {
  let sessionValid = true;
  let catalogosUnauthorized = false;

  await page.route('https://localhost:5173/api/**', async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;

    if (path === '/api/auth/me') {
      return sessionValid
        ? route.fulfill({
            json: { userId: 'admin-1', userName: 'Ana Administradora', roles: ['Administrador'] },
          })
        : route.fulfill({ status: 401 });
    }
    if (path === '/api/auth/csrf') {
      return route.fulfill({ json: { requestToken: 'csrf-401-test' } });
    }
    if (path === '/api/solicitudes/catalogos') {
      if (catalogosUnauthorized) {
        return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
      }
      return route.fulfill({ json: MOCK_CATALOGOS });
    }
    if (path === '/api/solicitudes' && req.method() === 'POST') {
      if (!sessionValid) {
        return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
      }
      return route.fulfill({
        status: 409,
        json: { code: 'Solicitud.IdempotenciaConflicto' },
      });
    }
    return route.abort();
  });

  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'CONF-409-1');

  // 409 IdempotenciaConflicto muestra alerta y desbloquea edición
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();
  await expect(page.getByRole('alert')).toContainText('conflicto de idempotencia');
  await expect(page.getByLabel(/^Folio/i)).toBeEnabled();

  // 401 durante envío invalida sesión y redirige a /admin/login
  sessionValid = false;
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);

  // 401 durante carga de catálogos también invalida sesión y redirige a /admin/login
  sessionValid = true;
  catalogosUnauthorized = true;
  await page.route('https://localhost:5173/api/solicitudes/catalogos', async (route) => {
    sessionValid = false;
    return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
  });
  await page.goto('/admin/solicitudes/nueva');
  await expect(page).toHaveURL(/\/admin\/login$/);
});

test('recuperación tras pérdida de respuesta + rechazo CSRF (400) conserva Idempotency-Key sin duplicar registro', async ({
  page,
}) => {
  let csrfCalls = 0;
  let activeCsrf = '';
  let postAttempts = 0;
  const seenKeys: string[] = [];
  const seenPayloads: string[] = [];
  const createdByIdempotencyKey = new Map<
    string,
    { id: string; folio: string; fechaVencimiento: string; diasRestantes: number; estatus: string }
  >();

  await page.route('https://localhost:5173/api/**', async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;

    if (path === '/api/auth/me') {
      return route.fulfill({
        json: { userId: 'admin-1', userName: 'Ana Administradora', roles: ['Administrador'] },
      });
    }
    if (path === '/api/auth/csrf') {
      csrfCalls += 1;
      activeCsrf = `csrf-rec-${csrfCalls}`;
      return route.fulfill({ json: { requestToken: activeCsrf } });
    }
    if (path === '/api/solicitudes/catalogos') {
      return route.fulfill({ json: MOCK_CATALOGOS });
    }
    if (path === '/api/solicitudes' && req.method() === 'POST') {
      postAttempts += 1;
      const key = req.headers()['idempotency-key'];
      seenKeys.push(key);
      seenPayloads.push(req.postData() ?? '');

      if (postAttempts === 1) {
        const body = req.postDataJSON();
        createdByIdempotencyKey.set(key, {
          id: 'sol-auto-persisted-1',
          folio: 'SOL-2026-001',
          fechaVencimiento: body.fechaVencimiento,
          diasRestantes: 10,
          estatus: body.estatusEntrega,
        });
        return route.abort('connectionfailed');
      }

      if (postAttempts === 2) {
        return route.fulfill({ status: 400, json: { title: 'CSRF inválido' } });
      }

      expect(req.headers()['x-csrf-token']).toBe('csrf-rec-2');
      const existing = createdByIdempotencyKey.get(key);
      if (existing) {
        return route.fulfill({ status: 200, json: existing });
      }
      const body = req.postDataJSON();
      const created = {
        id: 'sol-duplicada',
        folio: 'SOL-2026-002',
        fechaVencimiento: body.fechaVencimiento,
        diasRestantes: 10,
        estatus: body.estatusEntrega,
      };
      createdByIdempotencyKey.set(key, created);
      return route.fulfill({ status: 201, json: created });
    }
    return route.abort();
  });

  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'BORRADOR');
  await page.getByRole('button', { name: 'Generar folio automático' }).click();
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );

  // Primer Reintentar recibe 400 (CSRF expirado): conserva K1, payload y estado uncertain
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByRole('alert')).toContainText(
    'El token de seguridad expiró; se renovará al pulsar Reintentar conservando la misma operación',
  );
  await expect(page.getByLabel(/^Folio/i)).toBeDisabled();

  // Segundo Reintentar renueva CSRF, reutiliza K1 y recupera SOL-2026-001 con 1 solo registro creado
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByRole('status')).toContainText(
    'Solicitud SOL-2026-001 registrada correctamente',
  );
  expect(csrfCalls).toBe(2);
  expect(createdByIdempotencyKey.size).toBe(1);
  expect(seenKeys).toHaveLength(3);
  expect(seenKeys[0]).toBe(seenKeys[1]);
  expect(seenKeys[1]).toBe(seenKeys[2]);
  expect(seenPayloads[0]).toBe(seenPayloads[1]);
  expect(seenPayloads[1]).toBe(seenPayloads[2]);
});

test('recuperación tras pérdida de respuesta + 401: re-login mismo usuario recupera SOL-2026-001 y otro usuario descarta operación ajena', async ({
  page,
}) => {
  let activeUser: { userId: string; userName: string } | null = {
    userId: 'admin-1',
    userName: 'Ana Administradora',
  };
  let activeCsrf = 'csrf-init';
  let seq = 0;
  const createdByOwnerAndKey = new Map<
    string,
    { id: string; folio: string; fechaVencimiento: string; diasRestantes: number; estatus: string }
  >();
  const postLog: Array<{ userId: string; key: string; payload: string }> = [];

  await page.route('https://localhost:5173/api/**', async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;

    if (path === '/api/auth/me') {
      return activeUser
        ? route.fulfill({
            json: { userId: activeUser.userId, userName: activeUser.userName, roles: ['Administrador'] },
          })
        : route.fulfill({ status: 401 });
    }
    if (path === '/api/auth/csrf') {
      activeCsrf = `csrf-${randomUUID()}`;
      return route.fulfill({ json: { requestToken: activeCsrf } });
    }
    if (path === '/api/auth/login' && req.method() === 'POST') {
      const body = req.postDataJSON();
      activeUser =
        body.identifier === 'admin2'
          ? { userId: 'admin-2', userName: 'Beto Administrador' }
          : { userId: 'admin-1', userName: 'Ana Administradora' };
      return route.fulfill({ status: 200, body: '' });
    }
    if (path === '/api/auth/logout' && req.method() === 'POST') {
      activeUser = null;
      return route.fulfill({ status: 204 });
    }
    if (path === '/api/solicitudes/catalogos') {
      return activeUser
        ? route.fulfill({ json: MOCK_CATALOGOS })
        : route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
    }
    if (path === '/api/solicitudes' && req.method() === 'POST') {
      if (!activeUser) {
        return route.fulfill({ status: 401, json: { code: 'Auth.Unauthenticated' } });
      }
      const key = req.headers()['idempotency-key'];
      const payload = req.postData() ?? '';
      postLog.push({ userId: activeUser.userId, key, payload });

      const compositeKey = `${activeUser.userId}:${key}`;
      const existing = createdByOwnerAndKey.get(compositeKey);
      if (existing) {
        return route.fulfill({ status: 200, json: existing });
      }

      seq += 1;
      const body = req.postDataJSON();
      const record = {
        id: `sol-${seq}`,
        folio: `SOL-2026-00${seq}`,
        fechaVencimiento: body.fechaVencimiento,
        diasRestantes: 10,
        estatus: body.estatusEntrega,
      };
      createdByOwnerAndKey.set(compositeKey, record);
      return route.abort('connectionfailed');
    }
    return route.abort();
  });

  // Parte A: admin-1 pierde respuesta, expira sesión (401) en Reintentar y recupera tras re-login
  await page.goto('/admin/solicitudes/nueva');
  await fillValidForm(page, 'BORRADOR');
  await page.getByRole('button', { name: 'Generar folio automático' }).click();
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();

  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );

  // Expira sesión y al pulsar Reintentar recibe 401 -> redirige a /admin/login
  activeUser = null;
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);

  // Re-login con el MISMO usuario admin-1
  await page.getByLabel('Usuario o correo electrónico').fill('admin1');
  await page.locator('#login-password').fill('Secret123!');
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);

  await page.getByRole('link', { name: 'Nueva solicitud' }).click();
  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );
  await expect(page.getByLabel(/^Folio/i)).toBeDisabled();
  await expect(page.getByLabel(/^Folio/i)).toHaveValue('Se asignará al guardar');

  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page.getByRole('status')).toContainText(
    'Solicitud SOL-2026-001 registrada correctamente',
  );
  expect(createdByOwnerAndKey.size).toBe(1);
  expect(postLog).toHaveLength(2);
  expect(postLog[0].key).toBe(postLog[1].key);
  expect(postLog[0].payload).toBe(postLog[1].payload);

  // Parte B: nueva operación incierta de admin-1 -> 401 -> inicia sesión admin-2 y descarta operación ajena
  await page.getByRole('button', { name: 'Registrar otra solicitud' }).click();
  await fillValidForm(page, 'BORRADOR-2');
  await page.getByRole('button', { name: 'Generar folio automático' }).click();
  await page.getByRole('button', { name: 'Guardar' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar' }).click();
  await expect(page.getByRole('alert')).toContainText(
    'No fue posible completar la operación. Tus datos se conservaron; inténtalo de nuevo',
  );
  const keyAdmin1SecondOp = postLog[2].key;

  activeUser = null;
  await page.getByRole('button', { name: 'Reintentar' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);

  // Inicia sesión otro administrador: admin-2
  await page.getByLabel('Usuario o correo electrónico').fill('admin2');
  await page.locator('#login-password').fill('Secret123!');
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard$/);

  await page.getByRole('link', { name: 'Nueva solicitud' }).click();
  await expect(page.getByLabel(/^Folio/i)).toBeEnabled();
  await expect(page.getByLabel(/^Folio/i)).toHaveValue('');
  await expect(page.getByRole('alert')).toHaveCount(0);
  const storedAfterSwitch = await page.evaluate((storageKey) => {
    const storage = (
      globalThis as unknown as { sessionStorage: { getItem: (key: string) => string | null } }
    ).sessionStorage;
    return storage.getItem(storageKey);
  }, 'vyt.solicitudes.uncertainSubmission');
  expect(storedAfterSwitch).toBeNull();
  expect(postLog.some((entry) => entry.userId === 'admin-2' && entry.key === keyAdmin1SecondOp)).toBe(
    false,
  );
});
