using VyTSystems.Application.Solicitudes.Editar;

namespace VyTSystems.UnitTests.Application;

public sealed class EditarSolicitudValidatorTests
{
    private readonly EditarSolicitudValidator _validator = new();

    private static EditarSolicitudCommand ComandoValido() => new(
        UsuarioId: "user-admin-01",
        SolicitudId: "01950000-0000-7000-8000-000000000055",
        VersionConcurrencia: "0123456789abcdef0123456789abcdef",
        FechaEntrega: "2026-05-10",
        HoraEntrega: "09:30",
        AreaReceptoraId: "01950000-0000-7000-8000-000000000001",
        Asunto: "Solicitud actualizada de contrato público",
        MedioEntrega: "Digital",
        EstatusEntrega: "EnProceso",
        TipoSolicitud: "Informacion",
        TipoEvidencia: "Carpeta",
        TieneAcuse: true,
        TipoAcuse: "Sello",
        FechaVencimiento: "2026-05-20");

    [Fact]
    public void Validate_ComandoValidoYCasoBordeMismoDiaSinAcuse_Pasan()
    {
        Assert.True(_validator.Validate(ComandoValido()).IsValid);

        var borde = _validator.Validate(ComandoValido() with
        {
            HoraEntrega = "00:00",
            FechaVencimiento = "2026-05-10",
            TieneAcuse = false,
            TipoAcuse = "NoAplica",
            VersionConcurrencia = "etag-anterior"
        });
        Assert.True(borde.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("01950000-0000-7000-8000-000000000055\n")]
    public void Validate_SolicitudIdInvalido_FallaConNombrePropiedadId(string? solicitudId)
    {
        var result = _validator.Validate(ComandoValido() with { SolicitudId = solicitudId });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("\u0000")]
    [InlineData("etag con espacio")]
    [InlineData("\"con-comillas\"")]
    [InlineData("etag\nsalto")]
    public void Validate_VersionConcurrenciaInvalida_FallaConNombrePropiedadIfMatch(string? version)
    {
        var result = _validator.Validate(ComandoValido() with { VersionConcurrencia = version });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "IfMatch");
    }

    [Fact]
    public void Validate_VersionConcurrenciaMayorA128_FallaConNombrePropiedadIfMatch()
    {
        var result = _validator.Validate(ComandoValido() with { VersionConcurrencia = new string('a', 129) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "IfMatch");
    }

    [Fact]
    public void Validate_OnceCamposEditablesInvalidos_ReportanErroresEsperados()
    {
        var invalido = ComandoValido() with
        {
            FechaEntrega = "2026-05-20",
            HoraEntrega = "25:00",
            AreaReceptoraId = "00000000-0000-0000-0000-000000000000",
            Asunto = new string('z', 101),
            MedioEntrega = "0",
            EstatusEntrega = "Vencida",
            TipoSolicitud = "Inexistente",
            TipoEvidencia = "1",
            TieneAcuse = false,
            TipoAcuse = "Sello",
            FechaVencimiento = "2026-05-10"
        };

        var result = _validator.Validate(invalido);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.HoraEntrega));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.AreaReceptoraId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.Asunto));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.MedioEntrega));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.EstatusEntrega));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.TipoSolicitud));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.TipoEvidencia));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.TipoAcuse));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EditarSolicitudCommand.FechaVencimiento));
    }
}
