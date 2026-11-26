using VyTSystems.Application.Abstractions.Persistence;
using VyTSystems.Application.Common.Results;
using VyTSystems.Application.Dashboard.ObtenerResumen;

namespace VyTSystems.UnitTests.Application;

public sealed class ObtenerDashboardHandlerTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingDashboardReadRepository(
        Func<ConsultaDashboardParametros, CancellationToken, Task<LecturaDashboardOutcome>> responderAsync)
        : IDashboardReadRepository
    {
        public CapturingDashboardReadRepository(
            Func<ConsultaDashboardParametros, LecturaDashboardOutcome> responder)
            : this((p, _) => Task.FromResult(responder(p)))
        {
        }

        public ConsultaDashboardParametros? UltimosParametros { get; private set; }
        public CancellationToken UltimoCancellationToken { get; private set; }

        public Task<LecturaDashboardOutcome> ObtenerResumenAsync(
            ConsultaDashboardParametros parametros,
            CancellationToken cancellationToken = default)
        {
            UltimosParametros = parametros;
            UltimoCancellationToken = cancellationToken;
            return responderAsync(parametros, cancellationToken);
        }
    }

    private static DatosLecturaDashboard CrearDatosConsistentes(int total = 0, int? totalAnterior = 0) =>
        new(
            Total: total,
            EnProceso: total,
            Entregadas: 0,
            Vencidas: 0,
            ProximasAVencer: 0,
            TotalPeriodoAnterior: totalAnterior,
            PorArea: total > 0 ? [new DashboardAreaResumenDto("Finanzas", total)] : [],
            ProximosVencimientos: [],
            ActividadReciente: []);

    [Fact]
    public async Task Handle_CruceMedianocheUtc_ResuelveFechaReferenciaYRangoEnZonaHorariaMexico()
    {
        // 2026-06-01 03:30:00Z == 2026-05-31 21:30:00 America/Mexico_City (UTC-6)
        var antesMedianocheCdmx = new FixedTimeProvider(new DateTimeOffset(2026, 6, 1, 3, 30, 0, TimeSpan.Zero));
        var repoMayo = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(CrearDatosConsistentes(total: 8, totalAnterior: 4)));
        var handlerMayo = new ObtenerDashboardHandler(repoMayo, antesMedianocheCdmx);

        var resMayo = await handlerMayo.Handle(
            new ObtenerDashboardQuery(DashboardPeriodos.EsteMes),
            CancellationToken.None);

        Assert.True(resMayo.IsSuccess);
        Assert.NotNull(repoMayo.UltimosParametros);
        Assert.Equal(new DateOnly(2026, 5, 31), repoMayo.UltimosParametros!.HoyMexico);
        Assert.Equal(
            new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
            repoMayo.UltimosParametros.RangoActual);
        Assert.Equal(
            new RangoFechasDashboard(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)),
            repoMayo.UltimosParametros.RangoAnterior);
        Assert.Equal(new DateOnly(2026, 5, 31), resMayo.Value.FechaReferencia);
        Assert.Equal(new DateTime(2026, 6, 1, 3, 30, 0, DateTimeKind.Utc), resMayo.Value.ActualizadoEnUtc);

        // 2026-06-01 06:05:00Z == 2026-06-01 00:05:00 America/Mexico_City (ya es junio en México)
        var despuesMedianocheCdmx = new FixedTimeProvider(new DateTimeOffset(2026, 6, 1, 6, 5, 0, TimeSpan.Zero));
        var repoJunio = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(CrearDatosConsistentes(total: 2, totalAnterior: 8)));
        var handlerJunio = new ObtenerDashboardHandler(repoJunio, despuesMedianocheCdmx);

        var resJunio = await handlerJunio.Handle(
            new ObtenerDashboardQuery(DashboardPeriodos.EsteMes),
            CancellationToken.None);

        Assert.True(resJunio.IsSuccess);
        Assert.Equal(new DateOnly(2026, 6, 1), repoJunio.UltimosParametros!.HoyMexico);
        Assert.Equal(
            new RangoFechasDashboard(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            repoJunio.UltimosParametros.RangoActual);
        Assert.Equal(
            new RangoFechasDashboard(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
            repoJunio.UltimosParametros.RangoAnterior);
    }

    public static TheoryData<
        DateTimeOffset,
        string,
        DateOnly,
        DateOnly?,
        DateOnly?,
        DateOnly?,
        DateOnly?> CasosRangosPeriodos => new()
    {
        // Año bisiesto 2024 en febrero: este-mes termina el 29 de febrero
        {
            new DateTimeOffset(2024, 2, 15, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.EsteMes,
            new DateOnly(2024, 2, 15),
            new DateOnly(2024, 2, 1),
            new DateOnly(2024, 2, 29),
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31)
        },
        // Año bisiesto 2024 en marzo: mes-anterior cubre exactamente [2024-02-01, 2024-02-29]
        {
            new DateTimeOffset(2024, 3, 10, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.MesAnterior,
            new DateOnly(2024, 3, 10),
            new DateOnly(2024, 2, 1),
            new DateOnly(2024, 2, 29),
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31)
        },
        // Año no bisiesto 2026 en marzo: mes-anterior cubre [2026-02-01, 2026-02-28]
        {
            new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.MesAnterior,
            new DateOnly(2026, 3, 10),
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31)
        },
        // Cruce de año en febrero bisiesto para ultimos-3-meses: [2023-12-01, 2024-02-29] vs [2023-09-01, 2023-11-30]
        {
            new DateTimeOffset(2024, 2, 20, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.Ultimos3Meses,
            new DateOnly(2024, 2, 20),
            new DateOnly(2023, 12, 1),
            new DateOnly(2024, 2, 29),
            new DateOnly(2023, 9, 1),
            new DateOnly(2023, 11, 30)
        },
        // este-anio en 2026: [2026-01-01, 2026-12-31] vs [2025-01-01, 2025-12-31]
        {
            new DateTimeOffset(2026, 7, 19, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.EsteAnio,
            new DateOnly(2026, 7, 19),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31)
        },
        // todo: sin acotación de fechas ni periodo anterior
        {
            new DateTimeOffset(2026, 7, 19, 18, 0, 0, TimeSpan.Zero),
            DashboardPeriodos.Todo,
            new DateOnly(2026, 7, 19),
            null,
            null,
            null,
            null
        }
    };

    [Theory]
    [MemberData(nameof(CasosRangosPeriodos))]
    public async Task Handle_LosCincoPeriodosIncluyendoBisiesto_CalculaRangosInclusivosExactos(
        DateTimeOffset utcNow,
        string periodo,
        DateOnly hoyEsperado,
        DateOnly? inicioActualEsperado,
        DateOnly? finActualEsperado,
        DateOnly? inicioAnteriorEsperado,
        DateOnly? finAnteriorEsperado)
    {
        var clock = new FixedTimeProvider(utcNow);
        var repo = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(
                CrearDatosConsistentes(total: 10, totalAnterior: inicioAnteriorEsperado.HasValue ? 5 : null)));
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(periodo), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repo.UltimosParametros);
        Assert.Equal(hoyEsperado, repo.UltimosParametros!.HoyMexico);
        Assert.Equal(inicioActualEsperado, repo.UltimosParametros.RangoActual?.FechaInicio);
        Assert.Equal(finActualEsperado, repo.UltimosParametros.RangoActual?.FechaFin);
        Assert.Equal(inicioAnteriorEsperado, repo.UltimosParametros.RangoAnterior?.FechaInicio);
        Assert.Equal(finAnteriorEsperado, repo.UltimosParametros.RangoAnterior?.FechaFin);

        Assert.Equal(periodo, result.Value.Periodo);
        Assert.Equal(hoyEsperado, result.Value.FechaReferencia);
        Assert.Equal(inicioActualEsperado, result.Value.FechaInicio);
        Assert.Equal(finActualEsperado, result.Value.FechaFin);
        Assert.True(result.Value.Disponibilidad.Indicadores);
        Assert.True(result.Value.Disponibilidad.PorArea);
        Assert.True(result.Value.Disponibilidad.ProximosVencimientos);
        Assert.True(result.Value.Disponibilidad.ActividadReciente);
    }

    [Theory]
    [InlineData(DashboardPeriodos.EsteMes, 15, 10, 50.0)]
    [InlineData(DashboardPeriodos.EsteMes, 4, 3, 33.3)]
    [InlineData(DashboardPeriodos.EsteMes, 5, 3, 66.7)]
    [InlineData(DashboardPeriodos.EsteMes, 2001, 2000, 0.1)] // +0.05 -> AwayFromZero -> +0.1
    [InlineData(DashboardPeriodos.EsteMes, 1999, 2000, -0.1)] // -0.05 -> AwayFromZero -> -0.1
    [InlineData(DashboardPeriodos.MesAnterior, 7, 10, -30.0)]
    [InlineData(DashboardPeriodos.Ultimos3Meses, 1, 3, -66.7)]
    [InlineData(DashboardPeriodos.EsteAnio, 0, 8, -100.0)]
    [InlineData(DashboardPeriodos.EsteMes, 12, 12, 0.0)] // Variación cero real cuando ambos son 12
    public async Task Handle_VariacionPositivaNegativaYCero_RedondeaAUnDecimalAwayFromZero(
        string periodo,
        int totalActual,
        int totalAnterior,
        double variacionEsperada)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var repo = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(CrearDatosConsistentes(totalActual, totalAnterior)));
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(periodo), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((decimal)variacionEsperada, result.Value.Indicadores.VariacionTotalPorcentaje);
    }

    [Theory]
    [InlineData(DashboardPeriodos.Todo, 25, 10)]
    [InlineData(DashboardPeriodos.Todo, 0, null)]
    [InlineData(DashboardPeriodos.EsteMes, 15, 0)]
    [InlineData(DashboardPeriodos.EsteMes, 0, 0)]
    [InlineData(DashboardPeriodos.MesAnterior, 5, null)]
    public async Task Handle_EnPeriodoTodoOSinBaseAnteriorPositiva_OmiteVariacionConNullSinDividirEntreCero(
        string periodo,
        int totalActual,
        int? totalAnterior)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var repo = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(CrearDatosConsistentes(totalActual, totalAnterior)));
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(periodo), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Indicadores.VariacionTotalPorcentaje);
    }

    [Fact]
    public async Task Handle_MapeoCompletoConDatosReconciliados_ProyectaTodosLosIndicadoresYColecciones()
    {
        var ahoraUtc = new DateTimeOffset(2026, 5, 15, 18, 45, 30, TimeSpan.Zero);
        var hoyMexico = new DateOnly(2026, 5, 15);
        var clock = new FixedTimeProvider(ahoraUtc);

        IReadOnlyList<DashboardAreaResumenDto> porArea =
        [
            new("Finanzas", 5),
            new("Compras", 4),
            new("Jurídico", 3)
        ];

        IReadOnlyList<DashboardProximoVencimientoDto> proximosVencimientos =
        [
            new("SOL-2026-010", "Presupuesto anual", new DateOnly(2026, 5, 16), 1),
            new("SOL-2026-011", "Contrato de servicios", new DateOnly(2026, 5, 22), 7)
        ];

        IReadOnlyList<DashboardActividadRecienteDto> actividadReciente =
        [
            new(
                "NuevaSolicitud",
                "Nueva solicitud registrada",
                "SOL-2026-012",
                new DateTime(2026, 5, 15, 17, 10, 0, DateTimeKind.Utc)),
            new(
                "NuevaSolicitud",
                "Nueva solicitud registrada",
                "SOL-2026-011",
                new DateTime(2026, 5, 14, 16, 0, 0, DateTimeKind.Utc))
        ];

        var datosReales = new DatosLecturaDashboard(
            Total: 12,
            EnProceso: 6,
            Entregadas: 4,
            Vencidas: 2,
            ProximasAVencer: 3,
            TotalPeriodoAnterior: 8,
            PorArea: porArea,
            ProximosVencimientos: proximosVencimientos,
            ActividadReciente: actividadReciente);

        var repo = new CapturingDashboardReadRepository(_ => LecturaDashboardOutcome.Completada(datosReales));
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(DashboardPeriodos.EsteMes), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(DashboardPeriodos.EsteMes, dto.Periodo);
        Assert.Equal(hoyMexico, dto.FechaReferencia);
        Assert.Equal(new DateOnly(2026, 5, 1), dto.FechaInicio);
        Assert.Equal(new DateOnly(2026, 5, 31), dto.FechaFin);
        Assert.Equal(ahoraUtc.UtcDateTime, dto.ActualizadoEnUtc);

        Assert.True(dto.Disponibilidad.Indicadores);
        Assert.True(dto.Disponibilidad.PorArea);
        Assert.True(dto.Disponibilidad.ProximosVencimientos);
        Assert.True(dto.Disponibilidad.ActividadReciente);

        Assert.Equal(12, dto.Indicadores.Total);
        Assert.Equal(6, dto.Indicadores.EnProceso);
        Assert.Equal(4, dto.Indicadores.Entregadas);
        Assert.Equal(2, dto.Indicadores.Vencidas);
        Assert.Equal(3, dto.Indicadores.ProximasAVencer);
        Assert.Equal(50.0m, dto.Indicadores.VariacionTotalPorcentaje);

        Assert.Equal(porArea, dto.PorArea);
        Assert.Equal(proximosVencimientos, dto.ProximosVencimientos);
        Assert.Equal(actividadReciente, dto.ActividadReciente);
    }

    [Fact]
    public async Task Handle_EstadoVacioCompletoYColeccionesNulas_RetornaExitoConCerosYColeccionesVaciasNoNulas()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var datosVaciosConNulos = new DatosLecturaDashboard(
            Total: 0,
            EnProceso: 0,
            Entregadas: 0,
            Vencidas: 0,
            ProximasAVencer: 0,
            TotalPeriodoAnterior: 0,
            PorArea: null!,
            ProximosVencimientos: null!,
            ActividadReciente: null!);

        var repo = new CapturingDashboardReadRepository(_ => LecturaDashboardOutcome.Completada(datosVaciosConNulos));
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(DashboardPeriodos.EsteMes), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(0, dto.Indicadores.Total);
        Assert.Equal(0, dto.Indicadores.EnProceso);
        Assert.Equal(0, dto.Indicadores.Entregadas);
        Assert.Equal(0, dto.Indicadores.Vencidas);
        Assert.Equal(0, dto.Indicadores.ProximasAVencer);
        Assert.Null(dto.Indicadores.VariacionTotalPorcentaje);

        Assert.NotNull(dto.PorArea);
        Assert.Empty(dto.PorArea);
        Assert.NotNull(dto.ProximosVencimientos);
        Assert.Empty(dto.ProximosVencimientos);
        Assert.NotNull(dto.ActividadReciente);
        Assert.Empty(dto.ActividadReciente);
    }

    public static TheoryData<DatosLecturaDashboard> CasosDatosInconsistentes
    {
        get
        {
            var hoy = new DateOnly(2026, 5, 15);
            return new TheoryData<DatosLecturaDashboard>
            {
                // 1. Total != EnProceso + Entregadas + Vencidas
                new(
                    Total: 10,
                    EnProceso: 4,
                    Entregadas: 3,
                    Vencidas: 2,
                    ProximasAVencer: 0,
                    TotalPeriodoAnterior: 5,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 10)],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 2. ProximasAVencer > EnProceso
                new(
                    Total: 5,
                    EnProceso: 2,
                    Entregadas: 2,
                    Vencidas: 1,
                    ProximasAVencer: 3,
                    TotalPeriodoAnterior: 5,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 5)],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 3. Suma de PorArea != Total
                new(
                    Total: 6,
                    EnProceso: 3,
                    Entregadas: 2,
                    Vencidas: 1,
                    ProximasAVencer: 1,
                    TotalPeriodoAnterior: 4,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 4), new DashboardAreaResumenDto("Compras", 1)],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 4. Nombre de área en blanco
                new(
                    Total: 3,
                    EnProceso: 3,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 0,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("   ", 3)],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 5. Cantidad negativa en PorArea (aunque sume Total)
                new(
                    Total: 3,
                    EnProceso: 3,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 0,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 4), new DashboardAreaResumenDto("Compras", -1)],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 6. Conteos negativos en indicadores o TotalPeriodoAnterior
                new(
                    Total: 0,
                    EnProceso: -1,
                    Entregadas: 1,
                    Vencidas: 0,
                    ProximasAVencer: 0,
                    TotalPeriodoAnterior: 0,
                    PorArea: [],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                new(
                    Total: 0,
                    EnProceso: 0,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 0,
                    TotalPeriodoAnterior: -2,
                    PorArea: [],
                    ProximosVencimientos: [],
                    ActividadReciente: []),

                // 7. ProximosVencimientos con 0 días restantes (fuera de 1..7)
                new(
                    Total: 2,
                    EnProceso: 2,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 1,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 2)],
                    ProximosVencimientos: [new DashboardProximoVencimientoDto("SOL-1", "Asunto", hoy, 0)],
                    ActividadReciente: []),

                // 8. ProximosVencimientos con 8 días restantes (fuera de 1..7)
                new(
                    Total: 2,
                    EnProceso: 2,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 1,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 2)],
                    ProximosVencimientos: [new DashboardProximoVencimientoDto("SOL-1", "Asunto", hoy.AddDays(8), 8)],
                    ActividadReciente: []),

                // 9. ProximosVencimientos cuya FechaVencimiento no coincide con hoyMexico + DiasRestantes
                new(
                    Total: 2,
                    EnProceso: 2,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 1,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 2)],
                    ProximosVencimientos: [new DashboardProximoVencimientoDto("SOL-1", "Asunto", hoy.AddDays(5), 3)],
                    ActividadReciente: []),

                // 10. Cantidad de elementos en ProximosVencimientos excede ProximasAVencer o el máximo de 5
                new(
                    Total: 3,
                    EnProceso: 3,
                    Entregadas: 0,
                    Vencidas: 0,
                    ProximasAVencer: 1,
                    TotalPeriodoAnterior: 1,
                    PorArea: [new DashboardAreaResumenDto("Finanzas", 3)],
                    ProximosVencimientos:
                    [
                        new DashboardProximoVencimientoDto("SOL-1", "Asunto 1", hoy.AddDays(1), 1),
                        new DashboardProximoVencimientoDto("SOL-2", "Asunto 2", hoy.AddDays(2), 2)
                    ],
                    ActividadReciente: [])
            };
        }
    }

    [Theory]
    [MemberData(nameof(CasosDatosInconsistentes))]
    public async Task Handle_AnteDatosDeLecturaInconsistentes_LanzaInvalidOperationException(
        DatosLecturaDashboard datosInconsistentes)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var repo = new CapturingDashboardReadRepository(_ => LecturaDashboardOutcome.Completada(datosInconsistentes));
        var handler = new ObtenerDashboardHandler(repo, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ObtenerDashboardQuery(DashboardPeriodos.EsteMes), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PropagaCancellationTokenYOperationCanceledExceptionIntacta()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        using var ctsActiva = new CancellationTokenSource();

        var repoActivo = new CapturingDashboardReadRepository(_ =>
            LecturaDashboardOutcome.Completada(CrearDatosConsistentes(0, 0)));
        var handlerActivo = new ObtenerDashboardHandler(repoActivo, clock);

        var okResult = await handlerActivo.Handle(
            new ObtenerDashboardQuery(DashboardPeriodos.EsteMes),
            ctsActiva.Token);

        Assert.True(okResult.IsSuccess);
        Assert.Equal(ctsActiva.Token, repoActivo.UltimoCancellationToken);

        using var ctsCancelada = new CancellationTokenSource();
        ctsCancelada.Cancel();

        var repoCancelado = new CapturingDashboardReadRepository((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(LecturaDashboardOutcome.Completada(CrearDatosConsistentes(0, 0)));
        });
        var handlerCancelado = new ObtenerDashboardHandler(repoCancelado, clock);

        var cancelEx = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handlerCancelado.Handle(new ObtenerDashboardQuery(DashboardPeriodos.EsteMes), ctsCancelada.Token));

        Assert.Equal(ctsCancelada.Token, repoCancelado.UltimoCancellationToken);
        Assert.Equal(ctsCancelada.Token, cancelEx.CancellationToken);
    }

    [Fact]
    public async Task Handle_CuandoRepositorioReportaPersistenciaNoDisponible_RetornaFailurePersistenceUnavailable()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 5, 15, 18, 0, 0, TimeSpan.Zero));
        var repo = new CapturingDashboardReadRepository(_ => LecturaDashboardOutcome.PersistenciaNoDisponible());
        var handler = new ObtenerDashboardHandler(repo, clock);

        var result = await handler.Handle(new ObtenerDashboardQuery(DashboardPeriodos.EsteMes), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Failure, result.Error.Type);
        Assert.Equal("Persistence.Unavailable", result.Error.Code);
    }
}
