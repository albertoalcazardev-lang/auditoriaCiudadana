using VyTSystems.Domain.Common;
using VyTSystems.Domain.Entities;

namespace VyTSystems.UnitTests.Domain;

public sealed class AreaReceptoraTests
{
    [Fact]
    public void Crear_ConDatosValidos_NormalizaNombreYRespetaParametros()
    {
        var id = Guid.Parse("01950000-0000-7000-8000-000000000099");

        var area = AreaReceptora.Crear("  Dirección de Transparencia  ", orden: 3, activa: false, id: id);

        Assert.Equal(id, area.Id);
        Assert.Equal("Dirección de Transparencia", area.Nombre);
        Assert.Equal(3, area.Orden);
        Assert.False(area.Activa);
    }

    [Fact]
    public void Crear_SinId_GeneraGuidVersion7()
    {
        var area = AreaReceptora.Crear("Finanzas");

        Assert.NotEqual(Guid.Empty, area.Id);
        Assert.Equal(7, area.Id.Version);
        Assert.Equal(1, area.Orden);
        Assert.True(area.Activa);
    }

    [Fact]
    public void Crear_ConIdVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => AreaReceptora.Crear("Finanzas", 1, true, Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ConNombreVacio_LanzaDomainException(string? nombre)
    {
        Assert.Throws<DomainException>(() => AreaReceptora.Crear(nombre!));
    }

    [Fact]
    public void Crear_ConNombre150CaracteresTrasTrim_EsValido_Y151LanzaDomainException()
    {
        var nombre150 = "  " + new string('A', 150) + "  ";
        var area = AreaReceptora.Crear(nombre150);
        Assert.Equal(150, area.Nombre.Length);

        var nombre151 = new string('A', 151);
        Assert.Throws<DomainException>(() => AreaReceptora.Crear(nombre151));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_ConOrdenNoPositivo_LanzaDomainException(int orden)
    {
        Assert.Throws<DomainException>(() => AreaReceptora.Crear("Compras", orden));
    }
}
