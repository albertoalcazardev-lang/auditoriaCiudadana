using VyTSystems.Application.Solicitudes.Eliminar;

namespace VyTSystems.UnitTests.Application;

public sealed class EliminarSolicitudValidatorTests
{
    private readonly EliminarSolicitudValidator _validator = new();

    private static EliminarSolicitudCommand ComandoValido() => new(
        UsuarioId: "admin-user-01",
        SolicitudId: "01950000-0000-7000-8000-000000000055",
        VersionConcurrencia: "0123456789abcdef0123456789abcdef");

    [Fact]
    public void Validate_VersionValidaYVersionNula_PasanValidadorParaDelegar428AlHandler()
    {
        Assert.True(_validator.Validate(ComandoValido()).IsValid);
        Assert.True(_validator.Validate(ComandoValido() with { VersionConcurrencia = null }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("01950000-0000-7000-8000-000000000055\n")]
    public void Validate_SolicitudIdInvalido_FallaEnIdInclusoSinVersion(string? solicitudId)
    {
        var result = _validator.Validate(ComandoValido() with
        {
            SolicitudId = solicitudId,
            VersionConcurrencia = null
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("\u0000")]
    [InlineData("etag con espacio")]
    [InlineData("\"con-comillas\"")]
    [InlineData("etag\nsalto")]
    public void Validate_VersionConcurrenciaNoNulaMalformada_FallaEnIfMatch(string versionMalformada)
    {
        var result = _validator.Validate(ComandoValido() with { VersionConcurrencia = versionMalformada });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "IfMatch");
    }

    [Fact]
    public void Validate_VersionConcurrenciaMayorA128OUsuarioInvalido_Fallan()
    {
        var versionLarga = _validator.Validate(ComandoValido() with { VersionConcurrencia = new string('a', 129) });
        Assert.False(versionLarga.IsValid);
        Assert.Contains(versionLarga.Errors, e => e.PropertyName == "IfMatch");

        var usuarioInvalido = _validator.Validate(ComandoValido() with { UsuarioId = "   " });
        Assert.False(usuarioInvalido.IsValid);
        Assert.Contains(usuarioInvalido.Errors, e => e.PropertyName == nameof(EliminarSolicitudCommand.UsuarioId));
    }
}
