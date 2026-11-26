using VyTSystems.Domain.Common;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.UnitTests.Domain;

public sealed class SolicitudTests
{
    private static readonly Guid AreaIdValida = Guid.Parse("01950000-0000-7000-8000-000000000001");
    private static readonly DateTime UtcValido = new(2026, 5, 10, 15, 30, 0, DateTimeKind.Utc);

    private static Solicitud CrearSolicitudValida(
        string folio = "  0012-AbC/2026_1.0  ",
        DateOnly? fechaEntrega = null,
        TimeOnly? horaEntrega = null,
        Guid? areaId = null,
        string asunto = "  Solicitud de información pública  ",
        MedioEntrega medio = MedioEntrega.Digital,
        EstatusEntrega estatus = EstatusEntrega.EnProceso,
        TipoSolicitud tipoSolicitud = TipoSolicitud.Informacion,
        TipoEvidencia tipoEvidencia = TipoEvidencia.Carpeta,
        bool tieneAcuse = true,
        TipoAcuse tipoAcuse = TipoAcuse.Sello,
        DateOnly? fechaVencimiento = null,
        string usuarioId = "user-123",
        DateTime? creadoEnUtc = null,
        bool esAutomatico = false) =>
        Solicitud.Crear(
            folio,
            fechaEntrega ?? new DateOnly(2026, 5, 10),
            horaEntrega ?? new TimeOnly(14, 25, 48),
            areaId ?? AreaIdValida,
            asunto,
            medio,
            estatus,
            tipoSolicitud,
            tipoEvidencia,
            tieneAcuse,
            tipoAcuse,
            fechaVencimiento ?? new DateOnly(2026, 5, 20),
            usuarioId,
            creadoEnUtc ?? UtcValido,
            esAutomatico);

    [Fact]
    public void Crear_ConDatosValidos_PreservaCerosYCaseOriginalYNormalizaHoraYAsunto()
    {
        var solicitud = CrearSolicitudValida();

        Assert.NotEqual(Guid.Empty, solicitud.Id);
        Assert.Equal(7, solicitud.Id.Version);
        Assert.Equal("0012-AbC/2026_1.0", solicitud.Folio);
        Assert.Equal("0012-ABC/2026_1.0", solicitud.FolioNormalizado);
        Assert.False(solicitud.EsFolioAutomatico);
        Assert.Equal(new TimeOnly(14, 25), solicitud.HoraEntrega);
        Assert.Equal(0, solicitud.HoraEntrega.Second);
        Assert.Equal("Solicitud de información pública", solicitud.Asunto);
        Assert.Equal(DateTimeKind.Utc, solicitud.CreadoEnUtc.Kind);
    }

    [Fact]
    public void Crear_ConHoraMedianocheYFechasIguales_EsValido()
    {
        var fecha = new DateOnly(2026, 5, 10);
        var solicitud = CrearSolicitudValida(
            fechaEntrega: fecha,
            horaEntrega: new TimeOnly(0, 0),
            fechaVencimiento: fecha,
            tieneAcuse: false,
            tipoAcuse: TipoAcuse.NoAplica);

        Assert.Equal(new TimeOnly(0, 0), solicitud.HoraEntrega);
        Assert.Equal(fecha, solicitud.FechaEntrega);
        Assert.Equal(fecha, solicitud.FechaVencimiento);
    }

    [Fact]
    public void Crear_Asunto100CaracteresTrasTrimEsValido_Y101LanzaDomainException()
    {
        var asunto100 = "  " + new string('x', 100) + "  ";
        var solicitud = CrearSolicitudValida(asunto: asunto100);
        Assert.Equal(100, solicitud.Asunto.Length);

        var asunto101 = new string('x', 101);
        Assert.Throws<DomainException>(() => CrearSolicitudValida(asunto: asunto101));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("./_-")]
    [InlineData("SOL 001")]
    [InlineData("SOL@001")]
    [InlineData("SOL-001\n")]
    [InlineData("SOL\r001")]
    [InlineData("SOL\t001")]
    [InlineData("SOL-001\t")]
    [InlineData("SOL\0001")]
    public void Crear_ConFolioInvalido_LanzaDomainException(string folioInvalido)
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(folio: folioInvalido));
    }

    [Fact]
    public void Crear_ConFolioMayorA100Caracteres_LanzaDomainException()
    {
        var folio100 = new string('A', 100);
        var valida = CrearSolicitudValida(folio: folio100);
        Assert.Equal(100, valida.Folio.Length);

        var folio101 = new string('A', 101);
        Assert.Throws<DomainException>(() => CrearSolicitudValida(folio: folio101));
    }

    [Fact]
    public void Crear_ConFechasInvalidasOInvertidas_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(fechaEntrega: default(DateOnly)));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(fechaVencimiento: default(DateOnly)));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(
            fechaEntrega: new DateOnly(2026, 5, 10),
            fechaVencimiento: new DateOnly(2026, 5, 9)));
    }

    [Fact]
    public void Crear_ConAreaReceptoraVacia_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(areaId: Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Asunto\ncon salto")]
    [InlineData("Asunto\rcon retorno")]
    [InlineData("Asunto\tcon tab")]
    [InlineData("Asunto\0con nulo")]
    public void Crear_ConAsuntoVacioOControl_LanzaDomainException(string asunto)
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(asunto: asunto));
    }

    [Fact]
    public void Crear_ConEnumsNoDefinidos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(medio: (MedioEntrega)99));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(estatus: (EstatusEntrega)99));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(tipoSolicitud: (TipoSolicitud)99));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(tipoEvidencia: (TipoEvidencia)99));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(tipoAcuse: (TipoAcuse)99));
    }

    [Fact]
    public void Crear_ConAcuseIncoherente_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(tieneAcuse: false, tipoAcuse: TipoAcuse.Sello));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(tieneAcuse: true, tipoAcuse: TipoAcuse.NoAplica));
    }

    [Fact]
    public void Crear_ConUsuarioOFechaUtcInvalida_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: ""));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: "user\n1"));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: "user\r1"));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: "user\t1"));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: "user\01"));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(usuarioId: new string('u', 37)));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(creadoEnUtc: default(DateTime)));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(creadoEnUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Local)));
        Assert.Throws<DomainException>(() => CrearSolicitudValida(creadoEnUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void CalcularDiasRestantes_Y_ObtenerEstatusEfectivo_EvaluanHoyAyerYEntregada()
    {
        var vencimiento = new DateOnly(2026, 5, 20);
        var enProceso = CrearSolicitudValida(
            estatus: EstatusEntrega.EnProceso,
            fechaVencimiento: vencimiento);

        Assert.Equal(5, enProceso.CalcularDiasRestantes(new DateOnly(2026, 5, 15)));
        Assert.Equal(0, enProceso.CalcularDiasRestantes(vencimiento));
        Assert.Equal(0, enProceso.CalcularDiasRestantes(new DateOnly(2026, 5, 25)));

        Assert.Equal(EstatusEfectivo.EnProceso, enProceso.ObtenerEstatusEfectivo(new DateOnly(2026, 5, 19)));
        Assert.Equal(EstatusEfectivo.EnProceso, enProceso.ObtenerEstatusEfectivo(vencimiento));
        Assert.Equal(EstatusEfectivo.Vencida, enProceso.ObtenerEstatusEfectivo(new DateOnly(2026, 5, 21)));

        var entregada = CrearSolicitudValida(
            estatus: EstatusEntrega.Entregada,
            fechaVencimiento: vencimiento);

        Assert.Equal(EstatusEfectivo.Entregada, entregada.ObtenerEstatusEfectivo(new DateOnly(2026, 5, 21)));
    }

    [Fact]
    public void NormalizarFolio_Y_FormatearFolioAutomatico_FuncionanCorrectamente()
    {
        Assert.Equal("001-ABC/2026", Solicitud.NormalizarFolio("  001-abc/2026  "));
        Assert.Equal("SOL-2026-001", Solicitud.FormatearFolioAutomatico(2026, 1));
        Assert.Equal("SOL-2026-1024", Solicitud.FormatearFolioAutomatico(2026, 1024));

        Assert.Throws<DomainException>(() => Solicitud.NormalizarFolio("SOL\n01"));
        Assert.Throws<DomainException>(() => Solicitud.FormatearFolioAutomatico(0, 1));
        Assert.Throws<DomainException>(() => Solicitud.FormatearFolioAutomatico(10000, 1));
        Assert.Throws<DomainException>(() => Solicitud.FormatearFolioAutomatico(2026, 0));
    }

    [Theory]
    [InlineData("SOL-001", true)]
    [InlineData("  0012-AbC/2026_1.0  ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("SOL-001\n", false)]
    [InlineData("SOL\r001", false)]
    [InlineData("SOL-001\t", false)]
    [InlineData("\tSOL-001", false)]
    [InlineData("SOL\0001", false)]
    [InlineData("---", false)]
    public void EsFolioValido_EvaluaCorrectamenteFormatosYCaracteresDeControl(string? folio, bool esperado)
    {
        Assert.Equal(esperado, Solicitud.EsFolioValido(folio));
    }

    [Fact]
    public void ActualizarDatos_Con11CamposValidos_ActualizaEditablesYMantieneInmutables6AtributosDeIdentidadYFolio()
    {
        var solicitud = CrearSolicitudValida(
            folio: "  0012-AbC/2026_1.0  ",
            usuarioId: "admin-original-01",
            creadoEnUtc: UtcValido,
            esAutomatico: false);

        var idOriginal = solicitud.Id;
        var folioOriginal = solicitud.Folio;
        var folioNormalizadoOriginal = solicitud.FolioNormalizado;
        var esAutoOriginal = solicitud.EsFolioAutomatico;
        var usuarioOriginal = solicitud.CreadoPorUsuarioId;
        var creadoEnOriginal = solicitud.CreadoEnUtc;

        var areaAnterior = AreaReceptora.Crear("Área Original", 1, true, AreaIdValida);
        typeof(Solicitud).GetProperty(nameof(Solicitud.AreaReceptora))!
            .SetValue(solicitud, areaAnterior);

        var nuevaAreaId = Guid.Parse("01950000-0000-7000-8000-000000000002");
        var nuevaFechaEntrega = new DateOnly(2026, 6, 1);
        var nuevaFechaVencimiento = new DateOnly(2026, 6, 15);

        solicitud.ActualizarDatos(
            nuevaFechaEntrega,
            new TimeOnly(18, 45, 59),
            nuevaAreaId,
            "  Asunto editado y recortado  ",
            MedioEntrega.CorreoElectronico,
            EstatusEntrega.Entregada,
            TipoSolicitud.Aclaracion,
            TipoEvidencia.Link,
            false,
            TipoAcuse.NoAplica,
            nuevaFechaVencimiento);

        // 6 atributos estrictamente inmutables
        Assert.Equal(idOriginal, solicitud.Id);
        Assert.Equal(folioOriginal, solicitud.Folio);
        Assert.Equal(folioNormalizadoOriginal, solicitud.FolioNormalizado);
        Assert.Equal(esAutoOriginal, solicitud.EsFolioAutomatico);
        Assert.Equal(usuarioOriginal, solicitud.CreadoPorUsuarioId);
        Assert.Equal(creadoEnOriginal, solicitud.CreadoEnUtc);

        // 11 atributos editables actualizados + normalización de hora y asunto + navegación limpiada
        Assert.Equal(nuevaFechaEntrega, solicitud.FechaEntrega);
        Assert.Equal(new TimeOnly(18, 45), solicitud.HoraEntrega);
        Assert.Equal(0, solicitud.HoraEntrega.Second);
        Assert.Equal(nuevaAreaId, solicitud.AreaReceptoraId);
        Assert.Null(solicitud.AreaReceptora);
        Assert.Equal("Asunto editado y recortado", solicitud.Asunto);
        Assert.Equal(MedioEntrega.CorreoElectronico, solicitud.MedioEntrega);
        Assert.Equal(EstatusEntrega.Entregada, solicitud.EstatusEntrega);
        Assert.Equal(TipoSolicitud.Aclaracion, solicitud.TipoSolicitud);
        Assert.Equal(TipoEvidencia.Link, solicitud.TipoEvidencia);
        Assert.False(solicitud.TieneAcuse);
        Assert.Equal(TipoAcuse.NoAplica, solicitud.TipoAcuse);
        Assert.Equal(nuevaFechaVencimiento, solicitud.FechaVencimiento);

        // Recálculo coherente de días restantes y estatus efectivo
        Assert.Equal(5, solicitud.CalcularDiasRestantes(new DateOnly(2026, 6, 10)));
        Assert.Equal(EstatusEfectivo.Entregada, solicitud.ObtenerEstatusEfectivo(new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void ActualizarDatos_CasosBordePlazoAsuntoAreaEnumsYAcuse_ValidaIgualQueCrear()
    {
        var solicitud = CrearSolicitudValida();
        var mismaFecha = new DateOnly(2026, 7, 10);
        var asunto100 = "  " + new string('m', 100) + "  ";

        solicitud.ActualizarDatos(
            mismaFecha,
            new TimeOnly(0, 0),
            AreaIdValida,
            asunto100,
            MedioEntrega.Impreso,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Otro,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Folio,
            mismaFecha);

        Assert.Equal(mismaFecha, solicitud.FechaEntrega);
        Assert.Equal(mismaFecha, solicitud.FechaVencimiento);
        Assert.Equal(new TimeOnly(0, 0), solicitud.HoraEntrega);
        Assert.Equal(100, solicitud.Asunto.Length);
        Assert.Equal(EstatusEfectivo.Vencida, solicitud.ObtenerEstatusEfectivo(new DateOnly(2026, 7, 11)));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            default, new TimeOnly(10, 0), AreaIdValida, "Asunto", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, "Asunto", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, default));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            new DateOnly(2026, 7, 10), new TimeOnly(10, 0), AreaIdValida, "Asunto", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, new DateOnly(2026, 7, 9)));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), Guid.Empty, "Asunto", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, new string('x', 101), MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, "   ", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, "Asunto\ncon control", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, "Asunto", (MedioEntrega)77,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, false, TipoAcuse.NoAplica, mismaFecha));

        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            mismaFecha, new TimeOnly(10, 0), AreaIdValida, "Asunto", MedioEntrega.Digital,
            EstatusEntrega.EnProceso, TipoSolicitud.Informacion, TipoEvidencia.Carpeta, true, TipoAcuse.NoAplica, mismaFecha));
    }

    [Fact]
    public void ActualizarDatos_EsAtomicoEnMemoria_SiFallaUltimaValidacionNoMutaNingunaPropiedad()
    {
        var solicitud = CrearSolicitudValida(
            fechaEntrega: new DateOnly(2026, 5, 10),
            horaEntrega: new TimeOnly(14, 25),
            areaId: AreaIdValida,
            asunto: "Asunto inicial intacto",
            medio: MedioEntrega.Digital,
            estatus: EstatusEntrega.EnProceso,
            tipoSolicitud: TipoSolicitud.Informacion,
            tipoEvidencia: TipoEvidencia.Carpeta,
            tieneAcuse: true,
            tipoAcuse: TipoAcuse.Sello,
            fechaVencimiento: new DateOnly(2026, 5, 20));

        var areaCargada = AreaReceptora.Crear("Área Inicial", 1, true, AreaIdValida);
        typeof(Solicitud).GetProperty(nameof(Solicitud.AreaReceptora))!
            .SetValue(solicitud, areaCargada);

        var otraAreaId = Guid.Parse("01950000-0000-7000-8000-000000000009");

        // Todos los primeros campos son nuevos y válidos, pero falla la última regla (ValidarAcuse: false + Electronico)
        Assert.Throws<DomainException>(() => solicitud.ActualizarDatos(
            new DateOnly(2026, 8, 1),
            new TimeOnly(9, 10),
            otraAreaId,
            "Asunto que NO debe aplicarse",
            MedioEntrega.PortalWeb,
            EstatusEntrega.Entregada,
            TipoSolicitud.Aclaracion,
            TipoEvidencia.Link,
            tieneAcuse: false,
            tipoAcuse: TipoAcuse.Electronico,
            fechaVencimiento: new DateOnly(2026, 8, 15)));

        Assert.Equal(new DateOnly(2026, 5, 10), solicitud.FechaEntrega);
        Assert.Equal(new TimeOnly(14, 25), solicitud.HoraEntrega);
        Assert.Equal(AreaIdValida, solicitud.AreaReceptoraId);
        Assert.Same(areaCargada, solicitud.AreaReceptora);
        Assert.Equal("Asunto inicial intacto", solicitud.Asunto);
        Assert.Equal(MedioEntrega.Digital, solicitud.MedioEntrega);
        Assert.Equal(EstatusEntrega.EnProceso, solicitud.EstatusEntrega);
        Assert.Equal(TipoSolicitud.Informacion, solicitud.TipoSolicitud);
        Assert.Equal(TipoEvidencia.Carpeta, solicitud.TipoEvidencia);
        Assert.True(solicitud.TieneAcuse);
        Assert.Equal(TipoAcuse.Sello, solicitud.TipoAcuse);
        Assert.Equal(new DateOnly(2026, 5, 20), solicitud.FechaVencimiento);
    }
}
