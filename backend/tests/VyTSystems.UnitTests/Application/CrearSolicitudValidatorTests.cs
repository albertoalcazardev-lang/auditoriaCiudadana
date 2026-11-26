using VyTSystems.Application.Solicitudes.Crear;

namespace VyTSystems.UnitTests.Application;

public sealed class CrearSolicitudValidatorTests
{
    private readonly CrearSolicitudValidator _validator = new();

    private static CrearSolicitudCommand ComandoValidoManual() => new(
        UsuarioId: "user-admin-01",
        IdempotencyKey: "req_2026-01:abc.def",
        GenerarFolioAutomatico: false,
        Folio: "001-AbC/2026",
        FechaEntrega: "2026-05-10",
        HoraEntrega: "09:30",
        AreaReceptoraId: "01950000-0000-7000-8000-000000000001",
        Asunto: "Solicitud de contrato de obra pública",
        MedioEntrega: "Digital",
        EstatusEntrega: "EnProceso",
        TipoSolicitud: "Informacion",
        TipoEvidencia: "Carpeta",
        TieneAcuse: true,
        TipoAcuse: "Sello",
        FechaVencimiento: "2026-05-20");

    [Fact]
    public void Validate_ComandoManualYAutomaticoValidos_Pasan()
    {
        var manual = _validator.Validate(ComandoValidoManual());
        Assert.True(manual.IsValid);

        var automatico = _validator.Validate(ComandoValidoManual() with
        {
            GenerarFolioAutomatico = true,
            Folio = null,
            HoraEntrega = "00:00",
            FechaVencimiento = "2026-05-10",
            TieneAcuse = false,
            TipoAcuse = "NoAplica"
        });
        Assert.True(automatico.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("clave con espacio")]
    [InlineData("clave/invalida")]
    [InlineData("clave\n1")]
    [InlineData("clave\r1")]
    [InlineData("clave\t1")]
    [InlineData("clave\01")]
    public void Validate_IdempotencyKeyInvalida_Falla(string? idempotencyKey)
    {
        var result = _validator.Validate(ComandoValidoManual() with { IdempotencyKey = idempotencyKey });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_IdempotencyKeyMayorA100_Falla()
    {
        var result = _validator.Validate(ComandoValidoManual() with { IdempotencyKey = new string('a', 101) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.IdempotencyKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SOL-1")]
    public void Validate_ModalidadIncompatible_GenerarAutomaticoConFolioNoNulo_FallaEnFolio(string folio)
    {
        var result = _validator.Validate(ComandoValidoManual() with
        {
            GenerarFolioAutomatico = true,
            Folio = folio
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.Folio));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("SOL 001")]
    [InlineData("SOL-001\n")]
    [InlineData("SOL\r001")]
    [InlineData("SOL-001\t")]
    [InlineData("SOL\0001")]
    public void Validate_ModoManualConFolioInvalido_FallaEnFolio(string? folio)
    {
        var result = _validator.Validate(ComandoValidoManual() with
        {
            GenerarFolioAutomatico = false,
            Folio = folio
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.Folio));
    }

    [Theory]
    [InlineData("26/11/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    [InlineData("")]
    public void Validate_FechasMalformadas_Fallan(string fechaInvalida)
    {
        var r1 = _validator.Validate(ComandoValidoManual() with { FechaEntrega = fechaInvalida });
        Assert.Contains(r1.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.FechaEntrega));

        var r2 = _validator.Validate(ComandoValidoManual() with { FechaVencimiento = fechaInvalida });
        Assert.Contains(r2.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.FechaVencimiento));
    }

    [Fact]
    public void Validate_FechaVencimientoAnteriorAFechaEntrega_FallaEnFechaVencimiento()
    {
        var result = _validator.Validate(ComandoValidoManual() with
        {
            FechaEntrega = "2026-05-10",
            FechaVencimiento = "2026-05-09"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.FechaVencimiento));
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("12:00:00")]
    [InlineData("08:60")]
    [InlineData("")]
    public void Validate_HoraEntregaInvalida_Falla(string hora)
    {
        var result = _validator.Validate(ComandoValidoManual() with { HoraEntrega = hora });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.HoraEntrega));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_AreaReceptoraInvalida_Falla(string? areaId)
    {
        var result = _validator.Validate(ComandoValidoManual() with { AreaReceptoraId = areaId });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.AreaReceptoraId));
    }

    [Fact]
    public void Validate_Asunto100PasaY101Falla()
    {
        var ok = _validator.Validate(ComandoValidoManual() with { Asunto = "  " + new string('z', 100) + "  " });
        Assert.True(ok.IsValid);

        var fail = _validator.Validate(ComandoValidoManual() with { Asunto = new string('z', 101) });
        Assert.Contains(fail.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.Asunto));
    }

    [Theory]
    [InlineData("Asunto\n1")]
    [InlineData("Asunto\r1")]
    [InlineData("Asunto\t1")]
    [InlineData("Asunto\01")]
    public void Validate_AsuntoConCaracteresDeControl_Falla(string asunto)
    {
        var result = _validator.Validate(ComandoValidoManual() with { Asunto = asunto });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.Asunto));
    }

    [Theory]
    [InlineData("user\n1")]
    [InlineData("user\r1")]
    [InlineData("user\t1")]
    [InlineData("user\01")]
    public void Validate_UsuarioIdConCaracteresDeControl_Falla(string usuarioId)
    {
        var result = _validator.Validate(ComandoValidoManual() with { UsuarioId = usuarioId });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.UsuarioId));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("Digital, Digital")]
    public void Validate_EnumsNumericosOLista_Rechazados(string valorInvalido)
    {
        Assert.Contains(_validator.Validate(ComandoValidoManual() with { MedioEntrega = valorInvalido }).Errors,
            e => e.PropertyName == nameof(CrearSolicitudCommand.MedioEntrega));
        Assert.Contains(_validator.Validate(ComandoValidoManual() with { EstatusEntrega = valorInvalido }).Errors,
            e => e.PropertyName == nameof(CrearSolicitudCommand.EstatusEntrega));
        Assert.Contains(_validator.Validate(ComandoValidoManual() with { TipoSolicitud = valorInvalido }).Errors,
            e => e.PropertyName == nameof(CrearSolicitudCommand.TipoSolicitud));
        Assert.Contains(_validator.Validate(ComandoValidoManual() with { TipoEvidencia = valorInvalido }).Errors,
            e => e.PropertyName == nameof(CrearSolicitudCommand.TipoEvidencia));
        Assert.Contains(_validator.Validate(ComandoValidoManual() with { TipoAcuse = valorInvalido }).Errors,
            e => e.PropertyName == nameof(CrearSolicitudCommand.TipoAcuse));
    }

    [Theory]
    [InlineData("Vencida")]
    [InlineData("vencida")]
    public void Validate_EstatusEntregaVencida_EsRechazado(string estatus)
    {
        var result = _validator.Validate(ComandoValidoManual() with { EstatusEntrega = estatus });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.EstatusEntrega));
    }

    [Fact]
    public void Validate_CoherenciaAcuse_FallaCuandoEsInconsistente()
    {
        var sinAcuseConSello = _validator.Validate(ComandoValidoManual() with
        {
            TieneAcuse = false,
            TipoAcuse = "Sello"
        });
        Assert.Contains(sinAcuseConSello.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.TipoAcuse));

        var conAcuseNoAplica = _validator.Validate(ComandoValidoManual() with
        {
            TieneAcuse = true,
            TipoAcuse = "NoAplica"
        });
        Assert.Contains(conAcuseNoAplica.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.TipoAcuse));

        var tieneAcuseNulo = _validator.Validate(ComandoValidoManual() with { TieneAcuse = null });
        Assert.Contains(tieneAcuseNulo.Errors, e => e.PropertyName == nameof(CrearSolicitudCommand.TieneAcuse));
    }
}
