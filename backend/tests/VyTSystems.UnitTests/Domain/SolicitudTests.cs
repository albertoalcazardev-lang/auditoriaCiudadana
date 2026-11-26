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
}
