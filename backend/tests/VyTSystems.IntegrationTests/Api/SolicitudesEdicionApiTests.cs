using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Domain.Entities;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Api;

[Collection(MySqlIntegrationCollection.Name)]
public sealed partial class SolicitudesEdicionApiTests(MySqlTestFixture fixture)
{
    private const string AreaActiva1 = "01950000-0000-7000-8000-000000000001";
    private const string AreaActiva2 = "01950000-0000-7000-8000-000000000002";

    [GeneratedRegex(@"^""[0-9a-f]{32}""$", RegexOptions.CultureInvariant)]
    private static partial Regex StrongHexETagRegex();

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class UnavailableEdicionReadRepository : ISolicitudEdicionReadRepository
    {
        public Task<LecturaSolicitudEdicionOutcome> ObtenerPorFolioNormalizadoAsync(
            string folioNormalizado,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LecturaSolicitudEdicionOutcome.PersistenciaNoDisponible());
    }

    private sealed class ThrowingEdicionPersistence : ISolicitudEdicionPersistence
    {
        public Task<EdicionSolicitudOutcome> EjecutarEdicionAsync(
            EdicionSolicitudPersistRequest request,
            Action<Solicitud> actualizarSolicitud,
            CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Server=127.0.0.1;Password=TopSecret!;UPDATE solicitudes SET asunto='x';");
    }

    private static object PayloadCrear(string folio) => new
    {
        generarFolioAutomatico = false,
        folio,
        fechaEntrega = "2026-05-10",
        horaEntrega = "09:30",
        areaReceptoraId = AreaActiva1,
        asunto = "Asunto inicial para edición HTTP",
        medioEntrega = "Digital",
        estatusEntrega = "EnProceso",
        tipoSolicitud = "Informacion",
        tipoEvidencia = "Carpeta",
        tieneAcuse = true,
        tipoAcuse = "Sello",
        fechaVencimiento = "2026-05-20"
    };

    private static object PayloadActualizar(
        string asunto = "Asunto editado vía PUT",
        string areaReceptoraId = AreaActiva2,
        string estatusEntrega = "Entregada",
        bool tieneAcuse = false,
        string tipoAcuse = "NoAplica",
        string fechaEntrega = "2026-05-11",
        string fechaVencimiento = "2026-05-25") => new
        {
            fechaEntrega,
            horaEntrega = "16:05",
            areaReceptoraId,
            asunto,
            medioEntrega = "CorreoElectronico",
            estatusEntrega,
            tipoSolicitud = "Aclaracion",
            tipoEvidencia = "Link",
            tieneAcuse,
            tipoAcuse,
            fechaVencimiento
        };

    private static async Task<string> CrearSolicitudSemillaAsync(
        HttpClient adminClient,
        string csrfToken,
        string folio)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
        {
            Content = JsonContent.Create(PayloadCrear(folio))
        };
        req.Headers.Add("X-CSRF-TOKEN", csrfToken);
        req.Headers.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");

        var res = await adminClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private static async Task<HttpResponseMessage> PutSolicitudAsync(
        HttpClient client,
        string id,
        object payload,
        string? csrfToken,
        string? rawIfMatch)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/solicitudes/{id}")
        {
            Content = JsonContent.Create(payload)
        };

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
    public async Task Edicion_AutorizacionYCsrf_Retorna401_403_Y400Sin302()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();
        var sampleId = "01950000-0000-7000-8000-000000000050";

        // 1. Anónimo -> 401 en GET y PUT
        using (var anon = factory.CreateHttpsClientWithCookies())
        {
            var get401 = await anon.GetAsync("/api/solicitudes/edicion?folio=SOL-2026-001");
            Assert.Equal(HttpStatusCode.Unauthorized, get401.StatusCode);
            Assert.Contains("no-store", get401.Headers.CacheControl?.ToString());

            var put401 = await PutSolicitudAsync(anon, sampleId, PayloadActualizar(), null, "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.Unauthorized, put401.StatusCode);
        }

        // 2. Consulta -> 403 en GET y PUT
        using (var consulta = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                consulta,
                fixture.ConsultaUserName,
                fixture.ConsultaPassword,
                "Consulta");
            var csrfConsulta = await AuthTestClientHelper.ObtenerCsrfTokenAsync(consulta);

            var get403 = await consulta.GetAsync("/api/solicitudes/edicion?folio=SOL-2026-001");
            Assert.Equal(HttpStatusCode.Forbidden, get403.StatusCode);

            var put403 = await PutSolicitudAsync(
                consulta,
                sampleId,
                PayloadActualizar(),
                csrfConsulta,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.Forbidden, put403.StatusCode);
        }

        // 3. Administrador con CSRF ausente o inválido en PUT -> 400
        using (var admin = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                admin,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");

            var sinCsrf = await PutSolicitudAsync(
                admin,
                sampleId,
                PayloadActualizar(),
                null,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.BadRequest, sinCsrf.StatusCode);
            Assert.Contains("no-store", sinCsrf.Headers.CacheControl?.ToString());

            var malCsrf = await PutSolicitudAsync(
                admin,
                sampleId,
                PayloadActualizar(),
                "csrf-invalido",
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.BadRequest, malCsrf.StatusCode);
            Assert.Contains("no-store", malCsrf.Headers.CacheControl?.ToString());
        }
    }

    [Fact]
    public async Task FlujoCompleto_GetPrecargaConFolioEspecialYPutConETag_RotaETagManeja409Y404()
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

        const string folioOriginal = "00077-AbC/2026_1.0";
        var idCreado = await CrearSolicitudSemillaAsync(admin, csrf, folioOriginal);

        // 1. GET precarga case-insensitive preservando barras, guiones y ceros iniciales + ETag fuerte
        var encodedFolio = Uri.EscapeDataString("00077-abc/2026_1.0");
        var resGet = await admin.GetAsync($"/api/solicitudes/edicion?folio={encodedFolio}");
        Assert.Equal(HttpStatusCode.OK, resGet.StatusCode);
        Assert.Contains("no-store", resGet.Headers.CacheControl?.ToString());

        var etag1 = resGet.Headers.ETag?.ToString();
        Assert.NotNull(etag1);
        Assert.Matches(StrongHexETagRegex(), etag1!);

        var getBody = await resGet.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(idCreado, getBody.GetProperty("id").GetString());
        Assert.Equal(folioOriginal, getBody.GetProperty("folio").GetString());
        Assert.Equal("09:30", getBody.GetProperty("horaEntrega").GetString());
        Assert.Equal(AreaActiva1, getBody.GetProperty("areaReceptoraId").GetString());
        Assert.Equal(5, getBody.GetProperty("diasRestantes").GetInt32());
        Assert.Equal("EnProceso", getBody.GetProperty("estatusEfectivo").GetString());

        // 2. PUT exitoso con If-Match = etag1 -> 200 OK y nuevo ETag fuerte distinto
        var resPut = await PutSolicitudAsync(admin, idCreado, PayloadActualizar(), csrf, etag1);
        Assert.Equal(HttpStatusCode.OK, resPut.StatusCode);
        Assert.Contains("no-store", resPut.Headers.CacheControl?.ToString());

        var etag2 = resPut.Headers.ETag?.ToString();
        Assert.NotNull(etag2);
        Assert.Matches(StrongHexETagRegex(), etag2!);
        Assert.NotEqual(etag1, etag2);

        var putBody = await resPut.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(idCreado, putBody.GetProperty("id").GetString());
        Assert.Equal(folioOriginal, putBody.GetProperty("folio").GetString());
        Assert.Equal("16:05", putBody.GetProperty("horaEntrega").GetString());
        Assert.Equal(AreaActiva2, putBody.GetProperty("areaReceptoraId").GetString());
        Assert.Equal("Asunto editado vía PUT", putBody.GetProperty("asunto").GetString());
        Assert.Equal("Entregada", putBody.GetProperty("estatusEfectivo").GetString());
        Assert.Equal(10, putBody.GetProperty("diasRestantes").GetInt32());

        // 3. Reintentar PUT con versiones obsoletas (etag1, "0000...0000" y "etag-anterior") -> 409 Conflict sin sobrescribir
        foreach (var staleEtag in new[] { etag1!, "\"00000000000000000000000000000000\"", "\"etag-anterior\"" })
        {
            var res409 = await PutSolicitudAsync(
                admin,
                idCreado,
                PayloadActualizar(asunto: "Intento obsoleto rechazado"),
                csrf,
                staleEtag);
            Assert.Equal(HttpStatusCode.Conflict, res409.StatusCode);
            var json409 = await res409.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Solicitud.ConflictoConcurrencia", json409.GetProperty("code").GetString());
        }

        // Verificar que los datos vigentes siguen siendo los de etag2
        var verifyGet = await admin.GetAsync($"/api/solicitudes/edicion?folio={Uri.EscapeDataString(folioOriginal)}");
        Assert.Equal(etag2, verifyGet.Headers.ETag?.ToString());
        var verifyBody = await verifyGet.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Asunto editado vía PUT", verifyBody.GetProperty("asunto").GetString());

        // 4. 404 en GET por folio inexistente y en PUT tras eliminación del registro
        var get404 = await admin.GetAsync("/api/solicitudes/edicion?folio=FOLIO-NO-EXISTE-999");
        Assert.Equal(HttpStatusCode.NotFound, get404.StatusCode);
        var jsonGet404 = await get404.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.NoEncontrada", jsonGet404.GetProperty("code").GetString());

        await using (var db = fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM solicitudes WHERE id = {Guid.Parse(idCreado)};");
        }

        var put404 = await PutSolicitudAsync(admin, idCreado, PayloadActualizar(), csrf, etag2);
        Assert.Equal(HttpStatusCode.NotFound, put404.StatusCode);
        var jsonPut404 = await put404.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.NoEncontrada", jsonPut404.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Validaciones_IfMatchMalformadoFolioEnJsonYCamposInvalidos_Devuelven400SinMutarFolio()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();
        using var admin = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            admin,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(admin);

        const string folioOriginal = "INMUTABLE-2026-01";
        var idCreado = await CrearSolicitudSemillaAsync(admin, csrf, folioOriginal);
        var resGet = await admin.GetAsync($"/api/solicitudes/edicion?folio={folioOriginal}");
        var etagVigente = resGet.Headers.ETag!.ToString();

        // 1. GET con query folio ausente, vacío o repetido -> 400 errors.folio
        foreach (var url in new[]
                 {
                     "/api/solicitudes/edicion",
                     "/api/solicitudes/edicion?folio=",
                     "/api/solicitudes/edicion?folio=---",
                     "/api/solicitudes/edicion?folio=SOL-1&folio=SOL-2"
                 })
        {
            var badGet = await admin.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, badGet.StatusCode);
            var errGet = await badGet.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(errGet.GetProperty("errors").TryGetProperty("folio", out _));
        }

        // 2. If-Match ausente, débil (W/), comodín (* y "*"), sin comillas o lista múltiple -> 400 errors.ifMatch
        var sinIfMatch = await PutSolicitudAsync(admin, idCreado, PayloadActualizar(), csrf, null);
        Assert.Equal(HttpStatusCode.BadRequest, sinIfMatch.StatusCode);
        var errSinIfMatch = await sinIfMatch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(errSinIfMatch.GetProperty("errors").TryGetProperty("ifMatch", out _));

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
            var resBadIfMatch = await PutSolicitudAsync(admin, idCreado, PayloadActualizar(), csrf, badIfMatch);
            Assert.Equal(HttpStatusCode.BadRequest, resBadIfMatch.StatusCode);
            var errJson = await resBadIfMatch.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(errJson.GetProperty("errors").TryGetProperty("ifMatch", out _));
        }

        // 3. Intento de enviar "folio": "OTRO" en el JSON del PUT -> 400 Bad Request y folio intacto en BD
        var payloadConFolio = new
        {
            folio = "OTRO-FOLIO-PROHIBIDO",
            fechaEntrega = "2026-05-11",
            horaEntrega = "16:05",
            areaReceptoraId = AreaActiva2,
            asunto = "Intento mutar folio",
            medioEntrega = "Digital",
            estatusEntrega = "EnProceso",
            tipoSolicitud = "Informacion",
            tipoEvidencia = "Carpeta",
            tieneAcuse = false,
            tipoAcuse = "NoAplica",
            fechaVencimiento = "2026-05-25"
        };
        var resFolioProhibido = await PutSolicitudAsync(admin, idCreado, payloadConFolio, csrf, etagVigente);
        Assert.Equal(HttpStatusCode.BadRequest, resFolioProhibido.StatusCode);

        await using (var db = fixture.CreateDbContext())
        {
            var persistida = await db.Solicitudes.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(idCreado));
            Assert.Equal(folioOriginal, persistida.Folio);
            Assert.Equal("Asunto inicial para edición HTTP", persistida.Asunto);
        }

        // 4. Ruta con id no-GUID y campos editables inválidos + área inexistente
        var badIdRes = await PutSolicitudAsync(admin, "no-es-guid", PayloadActualizar(), csrf, etagVigente);
        Assert.Equal(HttpStatusCode.BadRequest, badIdRes.StatusCode);
        var badIdJson = await badIdRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(badIdJson.GetProperty("errors").TryGetProperty("id", out _));

        var badAreaRes = await PutSolicitudAsync(
            admin,
            idCreado,
            PayloadActualizar(areaReceptoraId: "01950000-0000-7000-8000-000000000999"),
            csrf,
            etagVigente);
        Assert.Equal(HttpStatusCode.BadRequest, badAreaRes.StatusCode);
        var badAreaJson = await badAreaRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(badAreaJson.GetProperty("errors").TryGetProperty("areaReceptoraId", out _));
    }

    [Fact]
    public async Task Edicion_FallaDePersistencia503SinFuga_YOpenApiConIfMatchYCorsETag()
    {
        // 1. 503 en GET y PUT sin fuga de detalles sensibles
        await using (var unavailableFactory = fixture.CreateWebApplicationFactory(
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<ISolicitudEdicionReadRepository, UnavailableEdicionReadRepository>());
                services.Replace(ServiceDescriptor.Scoped<ISolicitudEdicionPersistence, ThrowingEdicionPersistence>());
            }))
        {
            using var client = unavailableFactory.CreateHttpsClientWithCookies();
            await AuthTestClientHelper.IniciarSesionAsync(
                client,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");
            var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);

            var resGet503 = await client.GetAsync("/api/solicitudes/edicion?folio=SOL-503");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resGet503.StatusCode);
            var jsonGet503 = await resGet503.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Persistence.Unavailable", jsonGet503.GetProperty("code").GetString());

            var resPut503 = await PutSolicitudAsync(
                client,
                "01950000-0000-7000-8000-000000000050",
                PayloadActualizar(),
                csrf,
                "\"0123456789abcdef0123456789abcdef\"");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resPut503.StatusCode);
            var rawPut503 = await resPut503.Content.ReadAsStringAsync();
            Assert.DoesNotContain("TopSecret", rawPut503, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE", rawPut503, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Persistence.Unavailable", JsonDocument.Parse(rawPut503).RootElement.GetProperty("code").GetString());
        }

        // 2. OpenAPI en Development documenta GET /api/solicitudes/edicion y PUT /api/solicitudes/{id} con If-Match requerido
        await using (var devFactory = fixture.CreateWebApplicationFactory(environment: "Development"))
        {
            using var devClient = devFactory.CreateHttpsClientWithCookies();
            var openApiRes = await devClient.GetAsync("/openapi/v1.json");
            Assert.Equal(HttpStatusCode.OK, openApiRes.StatusCode);

            var openApiJson = await openApiRes.Content.ReadFromJsonAsync<JsonElement>();
            var paths = openApiJson.GetProperty("paths");
            Assert.True(paths.TryGetProperty("/api/solicitudes/edicion", out _));
            Assert.True(paths.TryGetProperty("/api/solicitudes/{id}", out var putPath));

            var putParams = putPath.GetProperty("put").GetProperty("parameters").EnumerateArray().ToArray();
            Assert.Contains(putParams, p =>
                string.Equals(p.GetProperty("name").GetString(), "If-Match", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.GetProperty("in").GetString(), "header", StringComparison.OrdinalIgnoreCase) &&
                p.GetProperty("required").GetBoolean());

            using var corsReq = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
            corsReq.Headers.Add("Origin", "http://localhost:5173");
            var corsRes = await devClient.SendAsync(corsReq);
            Assert.True(corsRes.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed));
            Assert.Contains(exposed!, v => v.Contains("ETag", StringComparison.OrdinalIgnoreCase));
        }
    }
}
