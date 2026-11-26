using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Api;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class DashboardApiTests(MySqlTestFixture fixture)
{
    private static readonly Guid[] AreasCatalogo =
    [
        Guid.Parse("01950000-0000-7000-8000-000000000001"), // Dirección de Obras Públicas
        Guid.Parse("01950000-0000-7000-8000-000000000002"), // Finanzas
        Guid.Parse("01950000-0000-7000-8000-000000000003"), // Compras
        Guid.Parse("01950000-0000-7000-8000-000000000004"), // Auditoría interna
        Guid.Parse("01950000-0000-7000-8000-000000000005"), // Tecnologías de la Información
        Guid.Parse("01950000-0000-7000-8000-000000000006"), // Recursos Humanos
        Guid.Parse("01950000-0000-7000-8000-000000000007"), // Jurídico
        Guid.Parse("01950000-0000-7000-8000-000000000008")  // Comisión Estatal del Agua
    ];

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private Solicitud CrearSemilla(
        string folio,
        DateOnly fechaEntrega,
        DateOnly fechaVencimiento,
        EstatusEntrega estatusEntrega,
        Guid areaReceptoraId,
        DateTime creadoEnUtc,
        string asunto = "Solicitud para pruebas HTTP Dashboard") =>
        Solicitud.Crear(
            folio,
            fechaEntrega,
            new TimeOnly(9, 15),
            areaReceptoraId,
            asunto,
            MedioEntrega.PortalWeb,
            estatusEntrega,
            TipoSolicitud.Informacion,
            TipoEvidencia.Link,
            true,
            TipoAcuse.Electronico,
            fechaVencimiento,
            fixture.AdminUserId,
            DateTime.SpecifyKind(creadoEnUtc, DateTimeKind.Utc),
            false);

    private async Task InsertarSolicitudesAsync(params Solicitud[] solicitudes)
    {
        await using var db = fixture.CreateDbContext();
        foreach (var solicitud in solicitudes)
        {
            var entry = db.Solicitudes.Add(solicitud);
            entry.Property("IdempotencyKey").CurrentValue = $"idem-{Guid.NewGuid():N}";
            entry.Property("IdempotencyPayloadHash").CurrentValue = new string('e', 64);
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Autorizacion_SinSesion401_RolConsulta403_RolAdministrador200ConNoStore()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();

        // 1. Sin sesión -> 401 ProblemDetails (sin redirección 302)
        using (var anonClient = factory.CreateHttpsClientWithCookies())
        {
            var res401 = await anonClient.GetAsync("/api/dashboard");
            Assert.Equal(HttpStatusCode.Unauthorized, res401.StatusCode);
            Assert.Contains("no-store", res401.Headers.CacheControl?.ToString());

            var body401 = await res401.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auth.Unauthenticated", body401.GetProperty("code").GetString());
        }

        // 2. Usuario autenticado con rol Consulta -> 403 ProblemDetails
        using (var consultaClient = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                consultaClient,
                fixture.ConsultaUserName,
                fixture.ConsultaPassword,
                "Consulta");

            var res403 = await consultaClient.GetAsync("/api/dashboard");
            Assert.Equal(HttpStatusCode.Forbidden, res403.StatusCode);
            Assert.Contains("no-store", res403.Headers.CacheControl?.ToString());

            var body403 = await res403.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auth.Forbidden", body403.GetProperty("code").GetString());
        }

        // 3. Usuario autenticado con rol Administrador -> 200 OK con encabezados no-store
        using (var adminClient = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                adminClient,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");

            var res200 = await adminClient.GetAsync("/api/dashboard");
            Assert.Equal(HttpStatusCode.OK, res200.StatusCode);
            Assert.Contains("no-store", res200.Headers.CacheControl?.ToString());
        }
    }

    [Fact]
    public async Task DashboardVacio_TablaSinSolicitudesYPeriodoSinSolicitudes_Retorna200ConCerosReales()
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

        // 1. Tabla completamente vacía
        var resEmptyTable = await admin.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, resEmptyTable.StatusCode);
        var jsonEmpty = await resEmptyTable.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("este-mes", jsonEmpty.GetProperty("periodo").GetString());
        Assert.Equal("2026-05-15", jsonEmpty.GetProperty("fechaReferencia").GetString());
        Assert.Equal("2026-05-01", jsonEmpty.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-05-31", jsonEmpty.GetProperty("fechaFin").GetString());
        Assert.Equal("2026-05-15T18:00:00+00:00", jsonEmpty.GetProperty("actualizadoEnUtc").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:sszzz"));

        var disp = jsonEmpty.GetProperty("disponibilidad");
        Assert.True(disp.GetProperty("indicadores").GetBoolean());
        Assert.True(disp.GetProperty("porArea").GetBoolean());
        Assert.True(disp.GetProperty("proximosVencimientos").GetBoolean());
        Assert.True(disp.GetProperty("actividadReciente").GetBoolean());

        var indEmpty = jsonEmpty.GetProperty("indicadores");
        Assert.Equal(0, indEmpty.GetProperty("total").GetInt32());
        Assert.Equal(0, indEmpty.GetProperty("enProceso").GetInt32());
        Assert.Equal(0, indEmpty.GetProperty("entregadas").GetInt32());
        Assert.Equal(0, indEmpty.GetProperty("vencidas").GetInt32());
        Assert.Equal(0, indEmpty.GetProperty("proximasAVencer").GetInt32());
        Assert.Equal(JsonValueKind.Null, indEmpty.GetProperty("variacionTotalPorcentaje").ValueKind);

        Assert.Equal(0, jsonEmpty.GetProperty("porArea").GetArrayLength());
        Assert.Equal(0, jsonEmpty.GetProperty("proximosVencimientos").GetArrayLength());
        Assert.Equal(0, jsonEmpty.GetProperty("actividadReciente").GetArrayLength());

        // 2. Periodo actual sin solicitudes cuando existen registros en mes anterior -> 200 OK y variación -100%
        await InsertarSolicitudesAsync(
            CrearSemilla(
                "ABR-SOLO-01",
                new DateOnly(2026, 4, 15),
                new DateOnly(2026, 4, 25),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc)));

        var resEmptyPeriod = await admin.GetAsync("/api/dashboard?periodo=este-mes");
        Assert.Equal(HttpStatusCode.OK, resEmptyPeriod.StatusCode);
        var jsonPeriod = await resEmptyPeriod.Content.ReadFromJsonAsync<JsonElement>();
        var indPeriod = jsonPeriod.GetProperty("indicadores");
        Assert.Equal(0, indPeriod.GetProperty("total").GetInt32());
        Assert.Equal(-100m, indPeriod.GetProperty("variacionTotalPorcentaje").GetDecimal());
        Assert.Equal(0, jsonPeriod.GetProperty("porArea").GetArrayLength());
    }

    [Fact]
    public async Task PeriodosValidosYDefault_ReconciliaIndicadoresAreasVencimientosYActividadReciente()
    {
        await fixture.ResetSolicitudesAsync();
        // 2026-05-15 18:00:00Z -> hoyMexico = 2026-05-15
        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateWebApplicationFactory(timeProvider: fixedClock);
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");

        await InsertarSolicitudesAsync(
            // Año 2025 (1 solicitud -> base anterior de este-anio, incluida en todo)
            CrearSemilla(
                "ANIO-2025-01",
                new DateOnly(2025, 8, 10),
                new DateOnly(2025, 8, 20),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2025, 8, 10, 15, 0, 0, DateTimeKind.Utc)),

            // Enero y Febrero 2026 (2 solicitudes -> base anterior [2025-12-01..2026-02-28] de ultimos-3-meses)
            CrearSemilla(
                "ENE-2026-01",
                new DateOnly(2026, 1, 15),
                new DateOnly(2026, 1, 30),
                EstatusEntrega.Entregada,
                AreasCatalogo[1],
                new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc)),
            CrearSemilla(
                "FEB-2026-01",
                new DateOnly(2026, 2, 10),
                new DateOnly(2026, 2, 20),
                EstatusEntrega.EnProceso,
                AreasCatalogo[1],
                new DateTime(2026, 2, 10, 15, 0, 0, DateTimeKind.Utc)),

            // Marzo 2026 (1 solicitud -> base anterior de mes-anterior, e incluida en ultimos-3-meses)
            CrearSemilla(
                "MAR-2026-01",
                new DateOnly(2026, 3, 12),
                new DateOnly(2026, 3, 25),
                EstatusEntrega.Entregada,
                AreasCatalogo[2],
                new DateTime(2026, 3, 12, 15, 0, 0, DateTimeKind.Utc)),

            // Abril 2026 (2 solicitudes -> mes-anterior y base anterior de este-mes)
            CrearSemilla(
                "ABR-2026-01",
                new DateOnly(2026, 4, 5),
                new DateOnly(2026, 4, 15),
                EstatusEntrega.Entregada,
                AreasCatalogo[2],
                new DateTime(2026, 4, 5, 15, 0, 0, DateTimeKind.Utc)),
            CrearSemilla(
                "ABR-2026-02",
                new DateOnly(2026, 4, 20),
                new DateOnly(2026, 4, 28),
                EstatusEntrega.EnProceso, // vencida respecto a 2026-05-15
                AreasCatalogo[3],
                new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc)),

            // Mayo 2026 (este-mes: 8 solicitudes distribuidas en 7 áreas para validar también agrupación > 6 áreas)
            // 1. Entregada con vencimiento pasado -> Entregadas (NO Vencida)
            CrearSemilla(
                "MAY-ENT-PAS",
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 6),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                "Entregada pasada"),
            // 2. Entregada con vencimiento en 2 días -> Entregadas (NO ProximosVencimientos)
            CrearSemilla(
                "MAY-ENT-PROX",
                new DateOnly(2026, 5, 10),
                new DateOnly(2026, 5, 17),
                EstatusEntrega.Entregada,
                AreasCatalogo[0],
                new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
                "Entregada cercana"),
            // 3. No entregada vencida -> Vencidas
            CrearSemilla(
                "MAY-VENC-01",
                new DateOnly(2026, 5, 2),
                new DateOnly(2026, 5, 10),
                EstatusEntrega.EnProceso,
                AreasCatalogo[1],
                new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc),
                "Vencida mayo"),
            // 4. No entregada que vence HOY (2026-05-15, 0 días) -> EnProceso, NO Vencida, NO ProximasAVencer
            CrearSemilla(
                "MAY-HOY-00",
                new DateOnly(2026, 5, 8),
                new DateOnly(2026, 5, 15),
                EstatusEntrega.EnProceso,
                AreasCatalogo[2],
                new DateTime(2026, 5, 8, 10, 0, 0, DateTimeKind.Utc),
                "Vence hoy"),
            // 5. Próxima a vencer en 1 día (2026-05-16)
            CrearSemilla(
                "MAY-PROX-1D",
                new DateOnly(2026, 5, 11),
                new DateOnly(2026, 5, 16),
                EstatusEntrega.EnProceso,
                AreasCatalogo[3],
                new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc),
                "Próxima 1 día"),
            // 6. Próxima a vencer en 4 días (2026-05-19)
            CrearSemilla(
                "MAY-PROX-4D",
                new DateOnly(2026, 5, 12),
                new DateOnly(2026, 5, 19),
                EstatusEntrega.EnProceso,
                AreasCatalogo[4],
                new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc),
                "Próxima 4 días"),
            // 7. Próxima a vencer en 7 días (2026-05-22)
            CrearSemilla(
                "MAY-PROX-7D",
                new DateOnly(2026, 5, 13),
                new DateOnly(2026, 5, 22),
                EstatusEntrega.EnProceso,
                AreasCatalogo[5],
                new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
                "Próxima 7 días"),
            // 8. En proceso con vencimiento en 10 días (2026-05-25) en 7ma área -> EnProceso, NO ProximasAVencer
            CrearSemilla(
                "MAY-FUT-10D",
                new DateOnly(2026, 5, 14),
                new DateOnly(2026, 5, 25),
                EstatusEntrega.EnProceso,
                AreasCatalogo[6],
                new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
                "Futura 10 días"));

        // 1. Default sin query param equivale exactamente a ?periodo=este-mes
        var resDefault = await admin.GetAsync("/api/dashboard");
        var resEsteMes = await admin.GetAsync("/api/dashboard?periodo=este-mes");
        Assert.Equal(HttpStatusCode.OK, resDefault.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resEsteMes.StatusCode);

        var rawDefault = await resDefault.Content.ReadAsStringAsync();
        var rawEsteMes = await resEsteMes.Content.ReadAsStringAsync();
        Assert.Equal(rawEsteMes, rawDefault);

        var esteMes = JsonDocument.Parse(rawEsteMes).RootElement;
        Assert.Equal("este-mes", esteMes.GetProperty("periodo").GetString());
        Assert.Equal("2026-05-15", esteMes.GetProperty("fechaReferencia").GetString());
        Assert.Equal("2026-05-01", esteMes.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-05-31", esteMes.GetProperty("fechaFin").GetString());

        var indMayo = esteMes.GetProperty("indicadores");
        Assert.Equal(8, indMayo.GetProperty("total").GetInt32());
        Assert.Equal(5, indMayo.GetProperty("enProceso").GetInt32()); // HOY + 1D + 4D + 7D + 10D
        Assert.Equal(2, indMayo.GetProperty("entregadas").GetInt32()); // ENT-PAS + ENT-PROX
        Assert.Equal(1, indMayo.GetProperty("vencidas").GetInt32()); // MAY-VENC-01
        Assert.Equal(3, indMayo.GetProperty("proximasAVencer").GetInt32()); // 1D, 4D, 7D
        // Base abril = 2, mayo = 8 -> ((8 - 2) / 2) * 100 = +300.0%
        Assert.Equal(300.0m, indMayo.GetProperty("variacionTotalPorcentaje").GetDecimal());

        // PorArea en mayo tiene 7 áreas distintas -> 6 principales + "Otras" y suma == 8
        var areasMayo = esteMes.GetProperty("porArea").EnumerateArray().ToArray();
        Assert.Equal(7, areasMayo.Length);
        Assert.Equal("Otras", areasMayo[6].GetProperty("area").GetString());
        Assert.Equal(8, areasMayo.Sum(a => a.GetProperty("cantidad").GetInt32()));

        // Próximos vencimientos en mayo: exactamente los 3 entre +1 y +7 días (excluye MAY-HOY-00, MAY-ENT-PROX y MAY-FUT-10D)
        var proxMayo = esteMes.GetProperty("proximosVencimientos").EnumerateArray().ToArray();
        Assert.Equal(3, proxMayo.Length);
        Assert.Equal(
            ["MAY-PROX-1D", "MAY-PROX-4D", "MAY-PROX-7D"],
            proxMayo.Select(p => p.GetProperty("folio").GetString()!).ToArray());
        Assert.Equal(
            [1, 4, 7],
            proxMayo.Select(p => p.GetProperty("diasRestantes").GetInt32()).ToArray());

        // Actividad reciente en mayo: top 5 ordenados descendentemente por CreadoEnUtc
        var actMayo = esteMes.GetProperty("actividadReciente").EnumerateArray().ToArray();
        Assert.Equal(5, actMayo.Length);
        Assert.Equal(
            ["MAY-FUT-10D", "MAY-PROX-7D", "MAY-PROX-4D", "MAY-PROX-1D", "MAY-ENT-PROX"],
            actMayo.Select(a => a.GetProperty("folio").GetString()!).ToArray());

        // 2. mes-anterior ([2026-04-01..2026-04-30] = 2 vs marzo = 1 -> +100.0%)
        var mesAnterior = await (await admin.GetAsync("/api/dashboard?periodo=mes-anterior"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mes-anterior", mesAnterior.GetProperty("periodo").GetString());
        Assert.Equal("2026-04-01", mesAnterior.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-04-30", mesAnterior.GetProperty("fechaFin").GetString());
        Assert.Equal(2, mesAnterior.GetProperty("indicadores").GetProperty("total").GetInt32());
        Assert.Equal(100.0m, mesAnterior.GetProperty("indicadores").GetProperty("variacionTotalPorcentaje").GetDecimal());

        // 3. ultimos-3-meses ([2026-03-01..2026-05-31] = 1 + 2 + 8 = 11 vs [2025-12-01..2026-02-28] = 2 -> +450.0%)
        var ultimos3 = await (await admin.GetAsync("/api/dashboard?periodo=ultimos-3-meses"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ultimos-3-meses", ultimos3.GetProperty("periodo").GetString());
        Assert.Equal("2026-03-01", ultimos3.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-05-31", ultimos3.GetProperty("fechaFin").GetString());
        Assert.Equal(11, ultimos3.GetProperty("indicadores").GetProperty("total").GetInt32());
        Assert.Equal(450.0m, ultimos3.GetProperty("indicadores").GetProperty("variacionTotalPorcentaje").GetDecimal());

        // 4. este-anio ([2026-01-01..2026-12-31] = 2 + 1 + 2 + 8 = 13 vs 2025 = 1 -> +1200.0%)
        var esteAnio = await (await admin.GetAsync("/api/dashboard?periodo=este-anio"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("este-anio", esteAnio.GetProperty("periodo").GetString());
        Assert.Equal("2026-01-01", esteAnio.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-12-31", esteAnio.GetProperty("fechaFin").GetString());
        Assert.Equal(13, esteAnio.GetProperty("indicadores").GetProperty("total").GetInt32());
        Assert.Equal(1200.0m, esteAnio.GetProperty("indicadores").GetProperty("variacionTotalPorcentaje").GetDecimal());

        // 5. todo (sin fechas de inicio/fin = null, total = 14, variacionTotalPorcentaje = null)
        var todo = await (await admin.GetAsync("/api/dashboard?periodo=todo"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("todo", todo.GetProperty("periodo").GetString());
        Assert.Equal(JsonValueKind.Null, todo.GetProperty("fechaInicio").ValueKind);
        Assert.Equal(JsonValueKind.Null, todo.GetProperty("fechaFin").ValueKind);
        Assert.Equal(14, todo.GetProperty("indicadores").GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, todo.GetProperty("indicadores").GetProperty("variacionTotalPorcentaje").ValueKind);
    }

    [Theory]
    [InlineData("/api/dashboard?periodo=")]
    [InlineData("/api/dashboard?periodo=%20%20")]
    [InlineData("/api/dashboard?periodo=invalido")]
    [InlineData("/api/dashboard?periodo=ESTE-MES")]
    [InlineData("/api/dashboard?periodo=este-mes&periodo=todo")]
    public async Task PeriodoInvalidoOVacioODuplicado_Retorna400ValidationProblemDetailsConErrorsPeriodo(string url)
    {
        await using var factory = fixture.CreateWebApplicationFactory();
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");

        var response = await admin.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("periodo", out var erroresPeriodo));
        Assert.True(erroresPeriodo.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task FallaDePersistenciaConCadenaConexionInvalida_Retorna503PersistenceUnavailableSinFiltrarDetalles()
    {
        var sharedDataProtection = DataProtectionProvider.Create("VyTSystems.Dashboard503Test");

        // 1. Emitimos una cookie real de Administrador usando el llavero compartido contra MySQL sano
        string authCookieHeader;
        await using (var healthyFactory = fixture.CreateWebApplicationFactory(
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(sharedDataProtection));
            }))
        {
            using var loginClient = healthyFactory.CreateDefaultClient(new Uri("https://localhost"));
            var csrfRes = await loginClient.GetAsync("/api/auth/csrf");
            csrfRes.EnsureSuccessStatusCode();
            var csrfCookies = csrfRes.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToArray();
            var csrfJson = await csrfRes.Content.ReadFromJsonAsync<JsonElement>();
            var csrfToken = csrfJson.GetProperty("requestToken").GetString()!;

            using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    identifier = fixture.AdminUserName,
                    password = fixture.AdminPassword,
                    requestedAccess = "Administrador",
                    rememberSession = false
                })
            };
            loginReq.Headers.Add("X-CSRF-TOKEN", csrfToken);
            loginReq.Headers.Add("Cookie", string.Join("; ", csrfCookies));

            var loginRes = await loginClient.SendAsync(loginReq);
            loginRes.EnsureSuccessStatusCode();

            var authSetCookie = loginRes.Headers.GetValues("Set-Cookie")
                .First(c => c.StartsWith("__Host-VyTSystems.Auth=", StringComparison.Ordinal));
            authCookieHeader = authSetCookie.Split(';')[0];
        }

        // 2. Creamos WebApplicationFactory con cadena de conexión MySQL inválida real (puerto cerrado + secreto)
        const string secretPassword = "SuperSecretDbPassword_9988!";
        var invalidConnectionString =
            $"Server=127.0.0.1;Port=39996;Database=secret_dashboard_db;Uid=db_admin;Pwd={secretPassword};Connection Timeout=1;";

        await using var brokenDbFactory = fixture.CreateWebApplicationFactory(
            connectionStringOverride: invalidConnectionString,
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(sharedDataProtection));
            });

        using var brokenClient = brokenDbFactory.CreateDefaultClient(new Uri("https://localhost"));
        using var dashboardReq = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard?periodo=este-mes");
        dashboardReq.Headers.Add("Cookie", authCookieHeader);

        var res503 = await brokenClient.SendAsync(dashboardReq);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res503.StatusCode);
        Assert.Contains("no-store", res503.Headers.CacheControl?.ToString());

        var raw503 = await res503.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secretPassword, raw503, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_dashboard_db", raw503, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("39996", raw503, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySqlException", raw503, StringComparison.OrdinalIgnoreCase);

        var json503 = JsonDocument.Parse(raw503).RootElement;
        Assert.Equal(503, json503.GetProperty("status").GetInt32());
        Assert.Equal("Persistence.Unavailable", json503.GetProperty("code").GetString());
    }
}
