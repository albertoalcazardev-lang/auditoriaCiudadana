import { expect, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';

// Pruebas del navegador: interceptar todas las llamadas de API; nunca usar cuentas de MySQL.
for (const [access, role, other, remember] of [
  ['admin', 'Administrador', 'consulta', true],
  ['consulta', 'Consulta', 'admin', false],
] as const) {
  test(`${role}: login, recarga, acceso cruzado y logout`, async ({ page }) => {
    let signedIn = false;
    let failLogout = true;
    let meReads = 0;
    let token = '';
    const calls: string[] = [];
    await page.route('https://localhost:5173/api/**', async route => {
      const request = route.request();
      const action = new URL(request.url()).pathname.split('/').pop()!;
      calls.push(action);
      if (action === 'csrf') {
        token = randomUUID();
        return route.fulfill({ json: { requestToken: token } });
      }
      if (action === 'me') {
        meReads++;
        return signedIn ? route.fulfill({ json: { userId: 'fixture', userName: 'Prueba', roles: [role] } })
          : route.fulfill({ status: 401 });
      }
      if (request.method() !== 'POST' || !token || request.headers()['x-csrf-token'] !== token)
        return route.fulfill({ status: 400 });
      if (action === 'login') {
        const body = request.postDataJSON();
        if (body.requestedAccess !== role || body.rememberSession !== remember || body.identifier !== 'fixture')
          return route.fulfill({ status: 401 });
        signedIn = true;
        token = '';
        return route.fulfill({ status: 200, body: '' });
      }
      if (action === 'logout') {
        if (failLogout) { failLogout = false; return route.abort('connectionfailed'); }
        signedIn = false;
        return route.fulfill({ status: 204 });
      }
      return route.abort();
    });
    await page.goto(`/${access}/dashboard`);
    await expect(page).toHaveURL(new RegExp(`/${access}/login$`));
    await page.getByLabel('Usuario o correo electrónico').fill('fixture');
    await page.locator('#login-password').fill(randomUUID());
    if (remember) await page.getByLabel('Recordar mi sesión').check();
    await page.getByRole('button', { name: 'Iniciar sesión' }).click();
    await expect(page).toHaveURL(new RegExp(`/${access}/dashboard$`));
    expect(calls.join(' ')).toContain('csrf login csrf me');
    const beforeReload = meReads;
    await page.reload();
    await expect(page.getByRole('heading', { name: `Panel de ${role}` })).toBeVisible();
    expect(meReads).toBeGreaterThan(beforeReload);
    await page.goto(`/${other}/dashboard`);
    await expect(page.getByRole('heading', { name: 'Acceso denegado' })).toBeVisible();
    await page.getByRole('link', { name: 'Ir a mi panel' }).click();
    await page.getByRole('button', { name: 'Cerrar sesión' }).click();
    await expect(page.getByRole('alert')).toContainText('conexión');
    await expect(page.getByRole('heading', { name: `Panel de ${role}` })).toBeVisible();
    await page.getByRole('button', { name: 'Cerrar sesión' }).click();
    await expect(page).toHaveURL(new RegExp(`/${access}/login$`));
    await page.goto(`/${access}/dashboard`);
    await expect(page).toHaveURL(new RegExp(`/${access}/login$`));
  });
}

test('la guarda espera a /me y no abre el panel ante un fallo', async ({ page }) => {
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/auth/me', async route => {
    await gate;
    return route.fulfill({ status: 503 });
  });
  await page.goto('/admin/dashboard');
  await expect(page.getByRole('status')).toContainText('Comprobando sesión');
  await expect(page.getByText('Panel de Administrador')).toHaveCount(0);
  release();
  await expect(page.getByRole('alert')).toContainText('temporalmente');
  await expect(page.getByText('Panel de Administrador')).toHaveCount(0);
});

test('rechazos de login: muestra errores seguros sin conceder sesión', async ({ page }) => {
  let status = 400;
  await page.route('https://localhost:5173/api/**', route => {
    const action = new URL(route.request().url()).pathname;
    if (action.endsWith('/csrf')) return route.fulfill({ json: { requestToken: randomUUID() } });
    return route.fulfill({ status: action.endsWith('/me') ? 401 : status, json: { detail: 'detalle interno' } });
  });
  await page.goto('/consulta/login');
  await page.getByLabel('Usuario o correo electrónico').fill('fixture');
  for (const [code, message] of [[400, 'solicitud no es válida'], [401, 'incorrectos'],
    [403, 'permiso'], [503, 'temporalmente']] as const) {
    status = code;
    await page.locator('#login-password').fill(randomUUID());
    await page.getByRole('button', { name: 'Iniciar sesión' }).click();
    await expect(page.getByRole('alert')).toContainText(message);
    await expect(page.getByRole('alert')).not.toContainText('detalle interno');
    await expect(page).toHaveURL(/\/consulta\/login$/);
  }
});
