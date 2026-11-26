using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Api;

[Collection(MySqlIntegrationCollection.Name)]
public sealed partial class SolicitudesListadoYEliminacionApiTests(MySqlTestFixture fixture)
{
    private const string AreaActiva1 = "01950000-0000-7000-8000-000000000001";
    private const string AreaActiva2 = "01950000-0000-7000-8000-000000000002";

    [GeneratedRegex(@"^""[0-9a-f]{32}""$", RegexOptions.CultureInvariant)]
    private static partial Regex StrongHexETagRegex();

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ThrowingListadoReadRepository : ISolicitudListadoReadRepository
    {
        public Task<LecturaSolicitudesListadoOutcome> ListarAsync(
            FiltroSolicitudesListado filtro,
            CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Server=127.0.0.1;Password=SuperSecretDb!;SELECT * FROM solicitudes;");
    }

    private sealed class ThrowingEliminacionPersistence : ISolicitudEliminacionPersistence
    {
        public Task<EliminacionSolicitudOutcome> EjecutarEliminacionAsync(
            EliminacionSolicitudPersistRequest request,
            CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Server=127.0.0.1;Password=SuperSecretDb!;DELETE FROM solicitudes;");
    }

    private static object PayloadCrear(
        bool automatico = false,
        string? folio = "SOL-2026-001",
        string fechaEntrega = "2026-05-10",
        string horaEntrega = "09:30",
        string areaReceptoraId = AreaActiva1,
        string asunto = "Asunto listado e integración",
        string medioEntrega = "Digital",
        string estatusEntrega = "EnProceso",
        string fechaVencimiento = "2026-05-20") => new
        {
            generarFolioAutomatico = automatico,
            folio = automatico ? null : folio,
            fechaEntrega,
            horaEntrega,
            areaReceptoraId,
            asunto,
            medioEntrega,
            estatusEntrega,
            tipoSolicitud = "Informacion",
            tipoEvidencia = "Carpeta",
            tieneAcuse = false,
            tipoAcuse = "NoAplica",
            fechaVencimiento
        };

    private static async Task<(string Id, string Folio)> CrearSemillaHttpAsync(
        HttpClient adminClient,
        string csrfToken,
        object payload,
        string? idempotencyKey = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("X-CSRF-TOKEN", csrfToken);
        req.Headers.Add("Idempotency-Key", idempotencyKey ?? $"idem-{Guid.NewGuid():N}");

        var res = await adminClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetString()!, json.GetProperty("folio").GetString()!);
    }

    private static async Task<HttpResponseMessage> DeleteSolicitudAsync(
        HttpClient client,
        string id,
        string? csrfToken,
        string? rawIfMatch)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/solicitudes/{id}");

        if (csrfToken is not null)
        {
            req.Headers.Add("X-CSRF-TOKEN", csrfToken);
        }

        if (rawIfMatch is not null)
        {
            req.Headers.TryAddWithoutValidation("If-Match", rawIfMatch);
        }

        return await client.SendAsync(req);
    }

    [Fact]
    public async Task AutorizacionYCsrf_EnGetListadoYDelete_Retorna401_403_Y400Sin302()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();
        const string sampleId = "01950000-0000-7000-8000-000000000050";

        // 1. Anónimo -> 401 en GET /api/solicitudes y DELETE /api/solicitudes/{id}
        using (var anon = factory.CreateHttpsClientWithCookies())
        {
            var get401 = await anon.GetAsync("/api/solicitudes");
            Assert.Equal(HttpStatusCode.Unauthorized, get401.StatusCode);
            Assert.Contains("no-store", get401.Headers.CacheControl?.ToString());

            var del401 = await DeleteSolicitudAsync(anon, sampleId, null, "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.Unauthorized, del401.StatusCode);
            Assert.Contains("no-store", del401.Headers.CacheControl?.ToString());
        }

        // 2. Rol Consulta -> 403 en GET y DELETE
        using (var consulta = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                consulta,
                fixture.ConsultaUserName,
                fixture.ConsultaPassword,
                "Consulta");
            var csrfConsulta = await AuthTestClientHelper.ObtenerCsrfTokenAsync(consulta);

            var get403 = await consulta.GetAsync("/api/solicitudes");
            Assert.Equal(HttpStatusCode.Forbidden, get403.StatusCode);

            var del403 = await DeleteSolicitudAsync(
                consulta,
                sampleId,
                csrfConsulta,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.Forbidden, del403.StatusCode);
        }

        // 3. Administrador con CSRF ausente o inválido en DELETE -> 400
        using (var admin = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                admin,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");

            var sinCsrf = await DeleteSolicitudAsync(
                admin,
                sampleId,
                null,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.BadRequest, sinCsrf.StatusCode);

            var malCsrf = await DeleteSolicitudAsync(
                admin,
                sampleId,
                "csrf-invalido-test",
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.BadRequest, malCsrf.StatusCode);
        }
    }

    [Fact]
    public async Task ListadoBusquedaYFiltrosCombinados_EnMySqlReal_ValidaFolioEspecialFechaImposible400YTodosLosRangosDias()
    {
        await fixture.ResetSolicitudesAsync();
        // 2026-05-15 18:00 UTC == 2026-05-15 12:00 CDMX -> hoyMexico = 2026-05-15
        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateWebApplicationFactory(timeProvider: fixedClock);
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(admin);

        // Semilla diversa alrededor de hoyMexico = 2026-05-15:
        // 1) Vencida ayer (2026-05-14), EnProceso -> Vencida, diasRestantes = 0
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "00010_AbC/2026",
            fechaEntrega: "2026-05-01",
            fechaVencimiento: "2026-05-14",
            estatusEntrega: "EnProceso",
            asunto: "Vencida real"));

        // 2) Vence HOY (2026-05-15), EnProceso -> EnProceso (NO Vencida), diasRestantes = 0
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "0010-AbC/2026",
            fechaEntrega: "2026-05-05",
            fechaVencimiento: "2026-05-15",
            estatusEntrega: "EnProceso",
            asunto: "Vence hoy mismo"));

        // 3) Vencimiento pasado (2026-05-10) pero Entregada -> Entregada, diasRestantes = 0
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "ENT-2026-003",
            fechaEntrega: "2026-05-02",
            fechaVencimiento: "2026-05-10",
            estatusEntrega: "Entregada",
            areaReceptoraId: AreaActiva2,
            asunto: "Ya entregada"));

        // 4) Vence en 2 días (2026-05-17) -> rango 1-3 y 1-7
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "R13-2026-004",
            fechaEntrega: "2026-05-10",
            fechaVencimiento: "2026-05-17",
            estatusEntrega: "EnProceso"));

        // 5) Vence en 6 días (2026-05-21) -> rango 4-7 y 1-7
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "R47-2026-005",
            fechaEntrega: "2026-05-11",
            fechaVencimiento: "2026-05-21",
            estatusEntrega: "EnProceso"));

        // 6) Vence en 10 días (2026-05-25) -> rango 8+
        await CrearSemillaHttpAsync(admin, csrf, PayloadCrear(
            folio: "R8P-2026-006",
            fechaEntrega: "2026-05-12",
            fechaVencimiento: "2026-05-25",
            estatusEntrega: "EnProceso"));

        // Búsqueda parcial de folio con '_', '/', ceros iniciales y case-insensitive
        var resFolioUnderscore = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/solicitudes?q={Uri.EscapeDataString("00010_abc/2026")}");
        Assert.Equal(1, resFolioUnderscore.GetProperty("totalItems").GetInt32());
        var itemUnderscore = resFolioUnderscore.GetProperty("items")[0];
        Assert.Equal("00010_AbC/2026", itemUnderscore.GetProperty("folio").GetString());
        Assert.Equal("Dirección de Obras Públicas", itemUnderscore.GetProperty("areaReceptora").GetString());
        Assert.Equal("Vencida", itemUnderscore.GetProperty("estatusEfectivo").GetString());
        Assert.Matches(StrongHexETagRegex(), itemUnderscore.GetProperty("version").GetString()!);

        // '%' literal en búsqueda parcial no devuelve todas las filas
        var resPorcentaje = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/solicitudes?q={Uri.EscapeDataString("%")}");
        Assert.Equal(0, resPorcentaje.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, resPorcentaje.GetProperty("totalPages").GetInt32());

        // Búsqueda por fecha exacta dd/mm/aaaa coincide con FechaEntrega (05/05/2026) o FechaVencimiento (21/05/2026)
        var resFechaExacta = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/solicitudes?q={Uri.EscapeDataString("05/05/2026")}");
        Assert.Equal(1, resFechaExacta.GetProperty("totalItems").GetInt32());
        Assert.Equal("0010-AbC/2026", resFechaExacta.GetProperty("items")[0].GetProperty("folio").GetString());
        Assert.Equal("EnProceso", resFechaExacta.GetProperty("items")[0].GetProperty("estatusEfectivo").GetString());
        Assert.Equal(0, resFechaExacta.GetProperty("items")[0].GetProperty("diasRestantes").GetInt32());

        // Fechas imposibles o sin formato dd/MM/yyyy exacto -> 400 Bad Request en errors.q (sin 500)
        foreach (var badDateQ in new[] { "31/02/2026", "29/02/2025", "99/99/2026", "5/3/2026" })
        {
            var badRes = await admin.GetAsync($"/api/solicitudes?q={Uri.EscapeDataString(badDateQ)}");
            Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
            var badJson = await badRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(badJson.GetProperty("errors").TryGetProperty("q", out _));
        }

        // Parámetros vacíos o repetidos -> 400
        foreach (var badQuery in new[]
                 {
                     "/api/solicitudes?dias=",
                     "/api/solicitudes?ent_d=",
                     "/api/solicitudes?q=SOL&q=OTRO",
                     "/api/solicitudes?page=1&page=2",
                     "/api/solicitudes?dias=0&dias=1-3"
                 })
        {
            var badRes = await admin.GetAsync(badQuery);
            Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
        }

        // dias=0 y dias=vencidas -> SOLO estatus efectivo Vencida (excluye la que vence hoy 0010-AbC/2026 y excluye Entregada)
        foreach (var filtroVencidas in new[] { "0", "vencidas" })
        {
            var resVencidas = await admin.GetFromJsonAsync<JsonElement>($"/api/solicitudes?dias={filtroVencidas}");
            Assert.Equal(1, resVencidas.GetProperty("totalItems").GetInt32());
            Assert.Equal("00010_AbC/2026", resVencidas.GetProperty("items")[0].GetProperty("folio").GetString());
        }

        // dias=1-3 -> R13-2026-004
        var res13 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes?dias=1-3");
        Assert.Equal(1, res13.GetProperty("totalItems").GetInt32());
        Assert.Equal("R13-2026-004", res13.GetProperty("items")[0].GetProperty("folio").GetString());

        // dias=4-7 -> R47-2026-005
        var res47 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes?dias=4-7");
        Assert.Equal(1, res47.GetProperty("totalItems").GetInt32());
        Assert.Equal("R47-2026-005", res47.GetProperty("items")[0].GetProperty("folio").GetString());

        // dias=1-7 (contrato compartido Task 05) -> R13 y R47
        var res17 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes?dias=1-7&sort=folio&dir=asc");
        Assert.Equal(2, res17.GetProperty("totalItems").GetInt32());
        var folios17 = res17.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("folio").GetString()).ToArray();
        Assert.Equal(new[] { "R13-2026-004", "R47-2026-005" }, folios17);

        // dias=8+ -> R8P-2026-006
        var res8p = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/solicitudes?dias={Uri.EscapeDataString("8+")}");
        Assert.Equal(1, res8p.GetProperty("totalItems").GetInt32());
        Assert.Equal("R8P-2026-006", res8p.GetProperty("items")[0].GetProperty("folio").GetString());

        // dias=custom con min=0..max=2 -> incluye vencida (00010_AbC/2026), vence hoy (0010-AbC/2026) y +2 días (R13-2026-004), excluyendo Entregada
        var resCustom02 = await admin.GetFromJsonAsync<JsonElement>(
            "/api/solicitudes?dias=custom&dias_min=0&dias_max=2&sort=vencimiento&dir=asc");
        Assert.Equal(3, resCustom02.GetProperty("totalItems").GetInt32());
        var foliosCustom02 = resCustom02.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("folio").GetString()).ToArray();
        Assert.Equal(new[] { "00010_AbC/2026", "0010-AbC/2026", "R13-2026-004" }, foliosCustom02);

        // Filtros combinados ent_d/ent_h + ven_d/ven_h + dias=custom(1..999)
        var resCombinado = await admin.GetFromJsonAsync<JsonElement>(
            "/api/solicitudes?ent_d=2026-05-10&ent_h=2026-05-11&ven_d=2026-05-17&ven_h=2026-05-22&dias=custom&dias_min=4&dias_max=999");
        Assert.Equal(1, resCombinado.GetProperty("totalItems").GetInt32());
        Assert.Equal("R47-2026-005", resCombinado.GetProperty("items")[0].GetProperty("folio").GetString());
    }

    [Fact]
    public async Task PaginacionYOrdenEstable_EnLasCuatroColumnas_NoDuplicaNiOmiteRegistrosEntrePaginas()
    {
        await fixture.ResetSolicitudesAsync();
        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateWebApplicationFactory(timeProvider: fixedClock);
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(admin);

        // Sembrar 27 registros con empates deliberados en FechaEntrega, HoraEntrega y FechaVencimiento
        const int totalSembrados = 27;
        var idsEsperados = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i <= totalSembrados; i++)
        {
            var diaGrupo = (i % 5) + 1; // 1..5 -> empates múltiples
            var fechaEnt = $"2026-05-{diaGrupo:D2}";
            var horaEnt = $"{8 + (i % 3):D2}:15";
            var fechaVen = $"2026-05-{13 + diaGrupo:D2}"; // 14..18 (vencidas, hoy y futuras)
            var estatus = i % 6 == 0 ? "Entregada" : "EnProceso";

            var (id, _) = await CrearSemillaHttpAsync(
                admin,
                csrf,
                PayloadCrear(
                    folio: $"PAG-2026-{i:D3}",
                    fechaEntrega: fechaEnt,
                    horaEntrega: horaEnt,
                    fechaVencimiento: fechaVen,
                    estatusEntrega: estatus));
            idsEsperados.Add(id);
        }

        // Tamaños 25 y 50
        var res25 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes?page=1&size=25");
        Assert.Equal(25, res25.GetProperty("items").GetArrayLength());
        Assert.Equal(totalSembrados, res25.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, res25.GetProperty("totalPages").GetInt32());

        var res50 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes?page=1&size=50");
        Assert.Equal(totalSembrados, res50.GetProperty("items").GetArrayLength());
        Assert.Equal(1, res50.GetProperty("totalPages").GetInt32());

        // Recorrer páginas de size=10 en las 4 columnas (asc y desc) verificando cobertura exacta sin duplicados
        foreach (var sort in new[] { "vencimiento", "folio", "entrega", "dias" })
        {
            foreach (var dir in new[] { "asc", "desc" })
            {
                var recolectados = new List<string>();
                for (var page = 1; page <= 3; page++)
                {
                    var pageJson = await admin.GetFromJsonAsync<JsonElement>(
                        $"/api/solicitudes?sort={sort}&dir={dir}&page={page}&size=10");
                    Assert.Equal(totalSembrados, pageJson.GetProperty("totalItems").GetInt32());
                    Assert.Equal(3, pageJson.GetProperty("totalPages").GetInt32());

                    foreach (var item in pageJson.GetProperty("items").EnumerateArray())
                    {
                        recolectados.Add(item.GetProperty("id").GetString()!);
                    }
                }

                Assert.Equal(totalSembrados, recolectados.Count);
                Assert.Equal(totalSembrados, recolectados.Distinct(StringComparer.Ordinal).Count());
                Assert.True(idsEsperados.SetEquals(recolectados));

                if (sort == "dias")
                {
                    // En sort=dias (asc y desc), todas las Entregadas quedan agrupadas al final y las no entregadas respetan diasRestantes
                    var fullJson = await admin.GetFromJsonAsync<JsonElement>(
                        $"/api/solicitudes?sort=dias&dir={dir}&page=1&size=50");
                    var fullItems = fullJson.GetProperty("items").EnumerateArray().ToArray();
                    var estatuses = fullItems.Select(x => x.GetProperty("estatusEfectivo").GetString()!).ToArray();

                    var primeraEntregada = Array.FindIndex(estatuses, e => e == "Entregada");
                    Assert.True(primeraEntregada > 0);
                    Assert.All(estatuses[primeraEntregada..], e => Assert.Equal("Entregada", e));

                    var diasNoEntregadas = fullItems[..primeraEntregada]
                        .Select(x => x.GetProperty("diasRestantes").GetInt32())
                        .ToArray();
                    var esperadoOrden = dir == "asc"
                        ? diasNoEntregadas.OrderBy(d => d).ToArray()
                        : diasNoEntregadas.OrderByDescending(d => d).ToArray();
                    Assert.Equal(esperadoOrden, diasNoEntregadas);
                }
            }
        }
    }

    [Fact]
    public async Task EliminacionControlada_204_428_412_404_400_409Integridad_PreservaSecuenciaFolioYBloqueaIdempotencyKey()
    {
        await fixture.ResetSolicitudesAsync();
        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateWebApplicationFactory(timeProvider: fixedClock);
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(admin);

        const string idemKeyEliminada = "idem-http-delete-001";
        var payloadAuto1 = PayloadCrear(automatico: true, asunto: "Solicitud automática 001 a eliminar");
        var (idAuto1, folioAuto1) = await CrearSemillaHttpAsync(admin, csrf, payloadAuto1, idemKeyEliminada);
        Assert.Equal("SOL-2026-001", folioAuto1);

        // Obtener versión desde el listado real
        var listado1 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes");
        var versionInicial = listado1.GetProperty("items")[0].GetProperty("version").GetString()!;
        Assert.Matches(StrongHexETagRegex(), versionInicial);

        // 1. 428 Precondition Required cuando If-Match está ausente (con type no nulo)
        var res428 = await DeleteSolicitudAsync(admin, idAuto1, csrf, null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, res428.StatusCode);
        var json428 = await res428.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.VersionRequerida", json428.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json428.GetProperty("type").GetString()));

        // 2. 400 Bad Request ante GUID inválido o If-Match débil / comodín / sin comillas
        var badGuidRes = await DeleteSolicitudAsync(admin, "no-es-guid", csrf, versionInicial);
        Assert.Equal(HttpStatusCode.BadRequest, badGuidRes.StatusCode);
        var badGuidJson = await badGuidRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(badGuidJson.GetProperty("errors").TryGetProperty("id", out _));

        foreach (var badIfMatch in new[]
                 {
                     "W/\"0123456789abcdef0123456789abcdef\"",
                     "*",
                     "\"*\"",
                     "0123456789abcdef0123456789abcdef",
                     "\"\"",
                     "\"a\", \"b\""
                 })
        {
            var res400 = await DeleteSolicitudAsync(admin, idAuto1, csrf, badIfMatch);
            Assert.Equal(HttpStatusCode.BadRequest, res400.StatusCode);
            var json400 = await res400.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json400.GetProperty("errors").TryGetProperty("ifMatch", out _));
        }

        // 3. Editar vía PUT para rotar versión y verificar que DELETE con versión obsoleta responde 412 sin borrar
        using (var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/solicitudes/{idAuto1}")
               {
                   Content = JsonContent.Create(new
                   {
                       fechaEntrega = "2026-05-10",
                       horaEntrega = "11:00",
                       areaReceptoraId = AreaActiva2,
                       asunto = "Asunto editado antes de borrar",
                       medioEntrega = "Digital",
                       estatusEntrega = "EnProceso",
                       tipoSolicitud = "Informacion",
                       tipoEvidencia = "Carpeta",
                       tieneAcuse = false,
                       tipoAcuse = "NoAplica",
                       fechaVencimiento = "2026-05-22"
                   })
               })
        {
            putReq.Headers.Add("X-CSRF-TOKEN", csrf);
            putReq.Headers.TryAddWithoutValidation("If-Match", versionInicial);
            var putRes = await admin.SendAsync(putReq);
            Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        }

        var res412 = await DeleteSolicitudAsync(admin, idAuto1, csrf, versionInicial);
        Assert.Equal(HttpStatusCode.PreconditionFailed, res412.StatusCode);
        var json412 = await res412.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.PrecondicionConcurrenciaFallida", json412.GetProperty("code").GetString());

        var listadoTras412 = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes");
        Assert.Equal(1, listadoTras412.GetProperty("totalItems").GetInt32());
        var versionRotada = listadoTras412.GetProperty("items")[0].GetProperty("version").GetString()!;
        Assert.NotEqual(versionInicial, versionRotada);

        // 4. Conflicto de integridad referencial 409 (MySqlException 1451 con tabla hija auxiliar temporal)
        const string auxChildTable = "aux_api_delete_restrict_test";
        await using (var dbSetup = fixture.CreateDbContext())
        {
            await dbSetup.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE `{auxChildTable}` (
                    `id` int NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    `solicitud_id` char(36) NOT NULL,
                    CONSTRAINT `FK_{auxChildTable}_solicitudes`
                        FOREIGN KEY (`solicitud_id`) REFERENCES `solicitudes` (`id`) ON DELETE RESTRICT
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            await dbSetup.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO `aux_api_delete_restrict_test` (`solicitud_id`) VALUES ({idAuto1});");
        }

        try
        {
            var res409Fk = await DeleteSolicitudAsync(admin, idAuto1, csrf, versionRotada);
            Assert.Equal(HttpStatusCode.Conflict, res409Fk.StatusCode);
            var json409Fk = await res409Fk.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Solicitud.ConflictoIntegridad", json409Fk.GetProperty("code").GetString());

            await using var dbVerify = fixture.CreateDbContext();
            Assert.True(await dbVerify.Solicitudes.AsNoTracking().AnyAsync(s => s.Id == Guid.Parse(idAuto1)));
            var tombstones = await dbVerify.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM solicitud_idempotencias_eliminadas WHERE idempotency_key = {0}",
                    idemKeyEliminada)
                .SingleAsync();
            Assert.Equal(0, tombstones);
        }
        finally
        {
            await using var dbCleanup = fixture.CreateDbContext();
            await dbCleanup.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS `{auxChildTable}`;");
        }

        // 5. Eliminación exitosa -> 204 No Content y segundo intento -> 404 Not Found
        var res204 = await DeleteSolicitudAsync(admin, idAuto1, csrf, versionRotada);
        Assert.Equal(HttpStatusCode.NoContent, res204.StatusCode);

        var res404 = await DeleteSolicitudAsync(admin, idAuto1, csrf, versionRotada);
        Assert.Equal(HttpStatusCode.NotFound, res404.StatusCode);
        var json404 = await res404.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.NoEncontrada", json404.GetProperty("code").GetString());

        // 6. Repetir POST con el mismo Idempotency-Key de la solicitud eliminada -> 409 Conflict y NO la recrea
        using (var replayDeletedReq = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
               {
                   Content = JsonContent.Create(payloadAuto1)
               })
        {
            replayDeletedReq.Headers.Add("X-CSRF-TOKEN", csrf);
            replayDeletedReq.Headers.Add("Idempotency-Key", idemKeyEliminada);

            var replayRes = await admin.SendAsync(replayDeletedReq);
            Assert.Equal(HttpStatusCode.Conflict, replayRes.StatusCode);
            var replayJson = await replayRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Solicitud.IdempotenciaRegistroEliminado", replayJson.GetProperty("code").GetString());
        }

        var listadoVacio = await admin.GetFromJsonAsync<JsonElement>("/api/solicitudes");
        Assert.Equal(0, listadoVacio.GetProperty("totalItems").GetInt32());

        // 7. Siguiente solicitud con folio automático conserva secuencia y recibe SOL-2026-002 (NO reutiliza 001)
        var (_, folioAuto2) = await CrearSemillaHttpAsync(
            admin,
            csrf,
            PayloadCrear(automatico: true, asunto: "Siguiente folio automático tras eliminación"));
        Assert.Equal("SOL-2026-002", folioAuto2);
    }

    [Fact]
    public async Task ListadoYEliminacion_FallaDePersistencia503SinFiltrarSecretos_YOpenApiDocumentaDeleteIfMatch()
    {
        await using (var unavailableFactory = fixture.CreateWebApplicationFactory(
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<ISolicitudListadoReadRepository, ThrowingListadoReadRepository>());
                services.Replace(ServiceDescriptor.Scoped<ISolicitudEliminacionPersistence, ThrowingEliminacionPersistence>());
            }))
        {
            using var admin = unavailableFactory.CreateHttpsClientWithCookies();
            await AuthTestClientHelper.IniciarSesionAsync(
                admin,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");
            var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(admin);

            var resList503 = await admin.GetAsync("/api/solicitudes");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resList503.StatusCode);
            var rawList503 = await resList503.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SuperSecretDb", rawList503, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT", rawList503, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Persistence.Unavailable", JsonDocument.Parse(rawList503).RootElement.GetProperty("code").GetString());

            var resDel503 = await DeleteSolicitudAsync(
                admin,
                "01950000-0000-7000-8000-000000000050",
                csrf,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resDel503.StatusCode);
            var rawDel503 = await resDel503.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SuperSecretDb", rawDel503, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE", rawDel503, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Persistence.Unavailable", JsonDocument.Parse(rawDel503).RootElement.GetProperty("code").GetString());
        }

        await using (var devFactory = fixture.CreateWebApplicationFactory(environment: "Development"))
        {
            using var devClient = devFactory.CreateHttpsClientWithCookies();
            var openApiRes = await devClient.GetAsync("/openapi/v1.json");
            Assert.Equal(HttpStatusCode.OK, openApiRes.StatusCode);

            var openApiJson = await openApiRes.Content.ReadFromJsonAsync<JsonElement>();
            var paths = openApiJson.GetProperty("paths");
            Assert.True(paths.TryGetProperty("/api/solicitudes", out var solicitudesPath));
            Assert.True(solicitudesPath.TryGetProperty("get", out _));

            Assert.True(paths.TryGetProperty("/api/solicitudes/{id}", out var idPath));
            var deleteParams = idPath.GetProperty("delete").GetProperty("parameters").EnumerateArray().ToArray();
            Assert.Contains(deleteParams, p =>
                string.Equals(p.GetProperty("name").GetString(), "If-Match", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.GetProperty("in").GetString(), "header", StringComparison.OrdinalIgnoreCase) &&
                p.GetProperty("required").GetBoolean());
        }
    }
}
