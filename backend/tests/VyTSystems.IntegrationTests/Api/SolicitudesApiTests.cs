using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Domain.Entities;
using VyTSystems.IntegrationTests.Fixtures;

namespace VyTSystems.IntegrationTests.Api;

[Collection(MySqlIntegrationCollection.Name)]
public sealed class SolicitudesApiTests(MySqlTestFixture fixture)
{
    private const string AreaIdValida = "01950000-0000-7000-8000-000000000001";

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ThrowingAreaReadRepository : IAreaReceptoraReadRepository
    {
        public Task<LecturaAreasReceptorasResult> ListarActivasAsync(CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Server=127.0.0.1;Password=Secr3t!;SELECT * FROM AspNetUsers;");
    }

    private sealed class UnavailableSolicitudPersistence : ISolicitudRegistroPersistence
    {
        public Task<RegistroSolicitudOutcome> EjecutarCreacionAsync(
            RegistroSolicitudPersistRequest request,
            Func<string, Solicitud> construirSolicitud,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RegistroSolicitudOutcome.PersistenciaNoDisponible());
    }

    private sealed class StubCatalogosFailureHandler :
        IRequestHandler<ObtenerCatalogosSolicitudQuery, Result<SolicitudesCatalogosDto>>
    {
        public Task<Result<SolicitudesCatalogosDto>> Handle(
            ObtenerCatalogosSolicitudQuery request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<SolicitudesCatalogosDto>(
                Error.Failure("Internal.Unexpected", "Detalle interno sensible")));
    }

    private sealed class StubCrearNotFoundHandler :
        IRequestHandler<CrearSolicitudCommand, Result<CrearSolicitudResult>>
    {
        public Task<Result<CrearSolicitudResult>> Handle(
            CrearSolicitudCommand request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure<CrearSolicitudResult>(
                Error.NotFound("Solicitud.AreaNoEncontrada", "Recurso no localizado.")));
    }

    private static object PayloadValidoManual(string folio = "0001-AbC/2026") => new
    {
        generarFolioAutomatico = false,
        folio,
        fechaEntrega = "2026-05-10",
        horaEntrega = "09:30",
        areaReceptoraId = AreaIdValida,
        asunto = "Solicitud de prueba HTTP",
        medioEntrega = "Digital",
        estatusEntrega = "EnProceso",
        tipoSolicitud = "Informacion",
        tipoEvidencia = "Carpeta",
        tieneAcuse = true,
        tipoAcuse = "Sello",
        fechaVencimiento = "2026-05-20"
    };

    private static async Task<HttpResponseMessage> PostSolicitudAsync(
        HttpClient client,
        object payload,
        string? csrfToken,
        string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
        {
            Content = JsonContent.Create(payload)
        };

        if (csrfToken is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Catalogos_SinSesionYConRolConsulta_Devuelve401Y403Sin302()
    {
        await using var factory = fixture.CreateWebApplicationFactory();

        using (var anonClient = factory.CreateHttpsClientWithCookies())
        {
            var res401 = await anonClient.GetAsync("/api/solicitudes/catalogos");
            Assert.Equal(HttpStatusCode.Unauthorized, res401.StatusCode);
            Assert.Contains("no-store", res401.Headers.CacheControl?.ToString());
            var json = await res401.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auth.Unauthenticated", json.GetProperty("code").GetString());
        }

        using (var consultaClient = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                consultaClient,
                fixture.ConsultaUserName,
                fixture.ConsultaPassword,
                "Consulta");

            var res403 = await consultaClient.GetAsync("/api/solicitudes/catalogos");
            Assert.Equal(HttpStatusCode.Forbidden, res403.StatusCode);
            Assert.Contains("no-store", res403.Headers.CacheControl?.ToString());
            var json = await res403.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Auth.Forbidden", json.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Catalogos_ConRolAdministrador_Devuelve10AreasYEnumsSinVencida()
    {
        await using var factory = fixture.CreateWebApplicationFactory();
        using var client = factory.CreateHttpsClientWithCookies();
        await AuthTestClientHelper.IniciarSesionAsync(
            client,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");

        var response = await client.GetAsync("/api/solicitudes/catalogos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var areas = json.GetProperty("areasReceptoras").EnumerateArray().ToArray();
        Assert.Equal(10, areas.Length);
        Assert.Equal(AreaIdValida, areas[0].GetProperty("id").GetString());
        Assert.Equal("Dirección de Obras Públicas", areas[0].GetProperty("nombre").GetString());

        var estatus = json.GetProperty("estatusEntrega").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(["EnProceso", "Entregada"], estatus);
        Assert.DoesNotContain("Vencida", estatus);
        Assert.Equal(4, json.GetProperty("mediosEntrega").GetArrayLength());
        Assert.Equal(3, json.GetProperty("tiposSolicitud").GetArrayLength());
        Assert.Equal(2, json.GetProperty("tiposEvidencia").GetArrayLength());
        Assert.Equal(4, json.GetProperty("tiposAcuse").GetArrayLength());
    }

    [Fact]
    public async Task CrearSolicitud_AutorizacionYCsrf_Retorna401_403_Y400()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();

        using (var anonClient = factory.CreateHttpsClientWithCookies())
        {
            var res401 = await PostSolicitudAsync(anonClient, PayloadValidoManual(), null, "k-anon");
            Assert.Equal(HttpStatusCode.Unauthorized, res401.StatusCode);
        }

        using (var consultaClient = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                consultaClient,
                fixture.ConsultaUserName,
                fixture.ConsultaPassword,
                "Consulta");
            var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(consultaClient);
            var res403 = await PostSolicitudAsync(consultaClient, PayloadValidoManual(), csrf, "k-cons");
            Assert.Equal(HttpStatusCode.Forbidden, res403.StatusCode);
        }

        using (var adminClient = factory.CreateHttpsClientWithCookies())
        {
            await AuthTestClientHelper.IniciarSesionAsync(
                adminClient,
                fixture.AdminUserName,
                fixture.AdminPassword,
                "Administrador");

            // CSRF ausente
            var sinCsrf = await PostSolicitudAsync(adminClient, PayloadValidoManual(), null, "k-no-csrf");
            Assert.Equal(HttpStatusCode.BadRequest, sinCsrf.StatusCode);
            Assert.Contains("no-store", sinCsrf.Headers.CacheControl?.ToString());

            // CSRF inválido
            var csrfInvalido = await PostSolicitudAsync(adminClient, PayloadValidoManual(), "token-falso", "k-bad-csrf");
            Assert.Equal(HttpStatusCode.BadRequest, csrfInvalido.StatusCode);
            Assert.Contains("no-store", csrfInvalido.Headers.CacheControl?.ToString());
        }
    }

    [Fact]
    public async Task CrearSolicitud_ManualAutomaticoReplayYConflictos409_FuncionanEndToEnd()
    {
        await fixture.ResetSolicitudesAsync();
        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 12, 18, 0, 0, TimeSpan.Zero));
        await using var factory = fixture.CreateWebApplicationFactory(timeProvider: fixedClock);
        using var client = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            client,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);

        // 1. 201 Created manual preservando ceros iniciales y casing
        var keyManual = $"idem-{Guid.NewGuid():N}";
        var resManual = await PostSolicitudAsync(client, PayloadValidoManual("00077-AbC/2026"), csrf, keyManual);
        Assert.Equal(HttpStatusCode.Created, resManual.StatusCode);
        Assert.Contains("no-store", resManual.Headers.CacheControl?.ToString());
        var bodyManual = await resManual.Content.ReadFromJsonAsync<JsonElement>();
        var idManual = bodyManual.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(idManual));
        Assert.Null(resManual.Headers.Location);
        Assert.Equal("00077-AbC/2026", bodyManual.GetProperty("folio").GetString());
        Assert.Equal("2026-05-20", bodyManual.GetProperty("fechaVencimiento").GetString());
        Assert.Equal(8, bodyManual.GetProperty("diasRestantes").GetInt32());
        Assert.Equal("EnProceso", bodyManual.GetProperty("estatus").GetString());

        // 2. 200 OK replay idempotente
        var resReplay = await PostSolicitudAsync(client, PayloadValidoManual("00077-AbC/2026"), csrf, keyManual);
        Assert.Equal(HttpStatusCode.OK, resReplay.StatusCode);
        var bodyReplay = await resReplay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(idManual, bodyReplay.GetProperty("id").GetString());
        Assert.Equal("00077-AbC/2026", bodyReplay.GetProperty("folio").GetString());

        // 3. 409 IdempotenciaConflicto (misma clave, distinto payload)
        var resIdempConflict = await PostSolicitudAsync(client, PayloadValidoManual("OTRO-FOLIO-999"), csrf, keyManual);
        Assert.Equal(HttpStatusCode.Conflict, resIdempConflict.StatusCode);
        var bodyIdempConflict = await resIdempConflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.IdempotenciaConflicto", bodyIdempConflict.GetProperty("code").GetString());

        // 4. 409 FolioDuplicado (nueva clave, mismo folio case-insensitive)
        var resFolioDup = await PostSolicitudAsync(client, PayloadValidoManual("00077-abc/2026"), csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Conflict, resFolioDup.StatusCode);
        var bodyFolioDup = await resFolioDup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.FolioDuplicado", bodyFolioDup.GetProperty("code").GetString());

        // 5. 201 Created automático
        var autoPayload = new
        {
            generarFolioAutomatico = true,
            folio = (string?)null,
            fechaEntrega = "2026-05-12",
            horaEntrega = "00:00",
            areaReceptoraId = AreaIdValida,
            asunto = "Solicitud automática HTTP",
            medioEntrega = "PortalWeb",
            estatusEntrega = "Entregada",
            tipoSolicitud = "Aclaracion",
            tipoEvidencia = "Link",
            tieneAcuse = false,
            tipoAcuse = "NoAplica",
            fechaVencimiento = "2026-05-12"
        };
        var resAuto = await PostSolicitudAsync(client, autoPayload, csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Created, resAuto.StatusCode);
        Assert.Null(resAuto.Headers.Location);
        var bodyAuto = await resAuto.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SOL-2026-001", bodyAuto.GetProperty("folio").GetString());
        Assert.Equal("Entregada", bodyAuto.GetProperty("estatus").GetString());
        Assert.Equal(0, bodyAuto.GetProperty("diasRestantes").GetInt32());
    }

    [Fact]
    public async Task CrearSolicitud_DatosInvalidosYAreaInexistente_Devuelve400CamelCase()
    {
        await fixture.ResetSolicitudesAsync();
        await using var factory = fixture.CreateWebApplicationFactory();
        using var client = factory.CreateHttpsClientWithCookies();

        await AuthTestClientHelper.IniciarSesionAsync(
            client,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);

        // 1. Campos inválidos (fechas, hora, asunto, enums numéricos/Vencida, acuse incoherente)
        var invalido = new
        {
            generarFolioAutomatico = true,
            folio = "NO-DEBE-VENIR",
            fechaEntrega = "26/11/2026",
            horaEntrega = "9:00",
            areaReceptoraId = AreaIdValida,
            asunto = new string('A', 101),
            medioEntrega = "1",
            estatusEntrega = "Vencida",
            tipoSolicitud = "Informacion",
            tipoEvidencia = "Carpeta",
            tieneAcuse = false,
            tipoAcuse = "Sello",
            fechaVencimiento = "2026-05-01"
        };
        var resInvalido = await PostSolicitudAsync(client, invalido, csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.BadRequest, resInvalido.StatusCode);
        var errJson = await resInvalido.Content.ReadFromJsonAsync<JsonElement>();
        var errors = errJson.GetProperty("errors");
        Assert.True(errors.TryGetProperty("folio", out _));
        Assert.True(errors.TryGetProperty("fechaEntrega", out _));
        Assert.True(errors.TryGetProperty("horaEntrega", out _));
        Assert.True(errors.TryGetProperty("asunto", out _));
        Assert.True(errors.TryGetProperty("medioEntrega", out _));
        Assert.True(errors.TryGetProperty("estatusEntrega", out _));
        Assert.True(errors.TryGetProperty("tipoAcuse", out _));

        // 2. Área receptora inexistente en BD -> 400 con errors.areaReceptoraId
        var areaInexistentePayload = new
        {
            generarFolioAutomatico = false,
            folio = "AREA-MISSING-1",
            fechaEntrega = "2026-05-10",
            horaEntrega = "10:00",
            areaReceptoraId = "01950000-0000-7000-8000-000000000999",
            asunto = "Prueba área inexistente",
            medioEntrega = "Digital",
            estatusEntrega = "EnProceso",
            tipoSolicitud = "Informacion",
            tipoEvidencia = "Carpeta",
            tieneAcuse = false,
            tipoAcuse = "NoAplica",
            fechaVencimiento = "2026-05-20"
        };
        var resArea = await PostSolicitudAsync(client, areaInexistentePayload, csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.BadRequest, resArea.StatusCode);
        var errArea = await resArea.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Revisa los campos indicados.", errArea.GetProperty("title").GetString());
        Assert.Equal("/api/solicitudes", errArea.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(errArea.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(errArea.GetProperty("traceId").GetString()));
        Assert.True(errArea.GetProperty("errors").TryGetProperty("areaReceptoraId", out _));

        // 3. JSON malformado
        using var badJsonReq = new HttpRequestMessage(HttpMethod.Post, "/api/solicitudes")
        {
            Content = new StringContent("{ \"generarFolioAutomatico\": \"no-bool\" ", Encoding.UTF8, "application/json")
        };
        badJsonReq.Headers.Add("X-CSRF-TOKEN", csrf);
        badJsonReq.Headers.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");
        var resBadJson = await client.SendAsync(badJsonReq);
        Assert.Equal(HttpStatusCode.BadRequest, resBadJson.StatusCode);
        Assert.Contains("no-store", resBadJson.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ServicioIndisponible_Devuelve503SinFugaDeDetallesInternos()
    {
        await using var customFactory = fixture.CreateWebApplicationFactory(
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IAreaReceptoraReadRepository, ThrowingAreaReadRepository>());
                services.Replace(ServiceDescriptor.Scoped<ISolicitudRegistroPersistence, UnavailableSolicitudPersistence>());
            });
        using var client = customFactory.CreateHttpsClientWithCookies();
        await AuthTestClientHelper.IniciarSesionAsync(
            client,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);

        // GlobalExceptionHandler ante TimeoutException con detalles internos simulados -> 503 sin fuga
        var resCatalogos = await client.GetAsync("/api/solicitudes/catalogos");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resCatalogos.StatusCode);
        Assert.Contains("no-store", resCatalogos.Headers.CacheControl?.ToString());
        var bodyCatalogos = await resCatalogos.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", bodyCatalogos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secr3t", bodyCatalogos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", bodyCatalogos, StringComparison.OrdinalIgnoreCase);
        var jsonCat = JsonDocument.Parse(bodyCatalogos).RootElement;
        Assert.Equal("Persistence.Unavailable", jsonCat.GetProperty("code").GetString());

        // Outcome PersistenciaNoDisponible desde handler -> 503
        var resCrear = await PostSolicitudAsync(client, PayloadValidoManual("503-TEST"), csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resCrear.StatusCode);
        Assert.Contains("no-store", resCrear.Headers.CacheControl?.ToString());
        var jsonCrear = await resCrear.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Persistence.Unavailable", jsonCrear.GetProperty("code").GetString());
    }

    [Fact]
    public async Task OpenApi_EnDevelopment_DocumentaRutasYIdempotencyKey()
    {
        await using var factory = fixture.CreateWebApplicationFactory(environment: "Development");
        using var client = factory.CreateHttpsClientWithCookies();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = json.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/solicitudes/catalogos", out _));
        Assert.True(paths.TryGetProperty("/api/solicitudes", out var solicitudesPath));

        var post = solicitudesPath.GetProperty("post");
        var parameters = post.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(parameters, p =>
            string.Equals(p.GetProperty("name").GetString(), "Idempotency-Key", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.GetProperty("in").GetString(), "header", StringComparison.OrdinalIgnoreCase) &&
            p.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task ErroresNotFoundYFailureInesperado_MapeanA404Y500ServerUnexpectedError()
    {
        await using var factory = fixture.CreateWebApplicationFactory(
            configureTestServices: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<
                    IRequestHandler<ObtenerCatalogosSolicitudQuery, Result<SolicitudesCatalogosDto>>,
                    StubCatalogosFailureHandler>());
                services.Replace(ServiceDescriptor.Scoped<
                    IRequestHandler<CrearSolicitudCommand, Result<CrearSolicitudResult>>,
                    StubCrearNotFoundHandler>());
            });

        using var client = factory.CreateHttpsClientWithCookies();
        await AuthTestClientHelper.IniciarSesionAsync(
            client,
            fixture.AdminUserName,
            fixture.AdminPassword,
            "Administrador");
        var csrf = await AuthTestClientHelper.ObtenerCsrfTokenAsync(client);

        var res500 = await client.GetAsync("/api/solicitudes/catalogos");
        Assert.Equal(HttpStatusCode.InternalServerError, res500.StatusCode);
        var body500 = await res500.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Detalle interno sensible", body500, StringComparison.Ordinal);
        var json500 = JsonDocument.Parse(body500).RootElement;
        Assert.Equal("Server.UnexpectedError", json500.GetProperty("code").GetString());

        var res404 = await PostSolicitudAsync(client, PayloadValidoManual("404-TEST"), csrf, $"idem-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, res404.StatusCode);
        var json404 = await res404.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Solicitud.AreaNoEncontrada", json404.GetProperty("code").GetString());
    }
}
