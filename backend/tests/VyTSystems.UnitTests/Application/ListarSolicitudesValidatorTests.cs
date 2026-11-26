using VyTSystems.Application.Solicitudes.Listar;

namespace VyTSystems.UnitTests.Application;

public sealed class ListarSolicitudesValidatorTests
{
    private readonly ListarSolicitudesValidator _validator = new();

    [Fact]
    public void Validate_ConsultaPorDefectoYValoresValidos_Pasan()
    {
        Assert.True(_validator.Validate(new ListarSolicitudesQuery()).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Q: "")).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Q: "   ")).IsValid);

        var completa = new ListarSolicitudesQuery(
            Q: "0045-AbC/2026_1",
            EntD: "2026-05-01",
            EntH: "2026-05-31",
            VenD: "2026-05-10",
            VenH: "2026-06-15",
            Dias: "1-7",
            DiasMin: null,
            DiasMax: null,
            Sort: "VENCIMIENTO",
            Dir: "DESC",
            Page: "2",
            Size: "25");

        Assert.True(_validator.Validate(completa).IsValid);
    }

    [Theory]
    [InlineData("15/05/2026")]
    [InlineData("29/02/2024")]
    [InlineData("01/01/1900")]
    [InlineData("  10/11/2026  ")]
    [InlineData("SOL_2026%01!")]
    [InlineData("12/2026")]
    [InlineData("---")]
    [InlineData("_")]
    public void Validate_BusquedaFechaExactaValidaOFolioParcial_Pasa(string q)
    {
        var result = _validator.Validate(new ListarSolicitudesQuery(Q: q));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("29/02/2025")]
    [InlineData("99/99/2026")]
    [InlineData("5/3/2026")]
    [InlineData("15/3/2026")]
    [InlineData("5/03/2026")]
    [InlineData("31/12/1899")]
    [InlineData("\u0000")]
    [InlineData("SOL\n2026")]
    [InlineData("SOL\t2026")]
    public void Validate_BusquedaConFechaImposibleOCaracterControl_FallaEnQ(string q)
    {
        var result = _validator.Validate(new ListarSolicitudesQuery(Q: q));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "q");
    }

    [Fact]
    public void Validate_BusquedaMayorA100Caracteres_FallaEnQ()
    {
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Q: " " + new string('A', 100) + " ")).IsValid);

        var fail = _validator.Validate(new ListarSolicitudesQuery(Q: new string('A', 101)));
        Assert.False(fail.IsValid);
        Assert.Contains(fail.Errors, e => e.PropertyName == "q");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000")]
    [InlineData("15/05/2026")]
    [InlineData("2026-02-30")]
    public void Validate_RangosDeFechaMalformados_FallanEnPropiedadCorrespondiente(string fechaInvalida)
    {
        Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(EntD: fechaInvalida)).Errors,
            e => e.PropertyName == "ent_d");
        Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(EntH: fechaInvalida)).Errors,
            e => e.PropertyName == "ent_h");
        Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(VenD: fechaInvalida)).Errors,
            e => e.PropertyName == "ven_d");
        Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(VenH: fechaInvalida)).Errors,
            e => e.PropertyName == "ven_h");
    }

    [Fact]
    public void Validate_RangosDeFechaInvertidosOIgueles_EvaluanCorrectamente()
    {
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(EntD: "2026-05-10", EntH: "2026-05-10")).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(VenD: "2026-05-10", VenH: "2026-05-10")).IsValid);

        var entInvertido = _validator.Validate(new ListarSolicitudesQuery(EntD: "2026-05-11", EntH: "2026-05-10"));
        Assert.False(entInvertido.IsValid);
        Assert.Contains(entInvertido.Errors, e => e.PropertyName == "ent_h");

        var venInvertido = _validator.Validate(new ListarSolicitudesQuery(VenD: "2026-06-02", VenH: "2026-06-01"));
        Assert.False(venInvertido.IsValid);
        Assert.Contains(venInvertido.Errors, e => e.PropertyName == "ven_h");
    }

    [Theory]
    [InlineData("todos")]
    [InlineData("TODOS")]
    [InlineData("0")]
    [InlineData("vencidas")]
    [InlineData("VENCIDAS")]
    [InlineData("1-3")]
    [InlineData("4-7")]
    [InlineData("1-7")]
    [InlineData("8+")]
    public void Validate_FiltroDiasPredefinidoValido_Pasa(string dias)
    {
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Dias: dias)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000")]
    [InlineData("9-15")]
    [InlineData("vencida")]
    public void Validate_FiltroDiasInvalido_FallaEnDias(string diasInvalido)
    {
        var result = _validator.Validate(new ListarSolicitudesQuery(Dias: diasInvalido));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "dias");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("todos")]
    [InlineData("0")]
    [InlineData("1-7")]
    public void Validate_DiasMinOMaxSinModoCustom_Falla(string? dias)
    {
        var rMin = _validator.Validate(new ListarSolicitudesQuery(Dias: dias, DiasMin: "1"));
        Assert.False(rMin.IsValid);
        Assert.Contains(rMin.Errors, e => e.PropertyName == "dias_min");

        var rMax = _validator.Validate(new ListarSolicitudesQuery(Dias: dias, DiasMax: "10"));
        Assert.False(rMax.IsValid);
        Assert.Contains(rMax.Errors, e => e.PropertyName == "dias_max");
    }

    [Fact]
    public void Validate_DiasCustom_ReglasDeLimites0A999YOrden()
    {
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Dias: "custom", DiasMin: "0")).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Dias: "custom", DiasMax: "999")).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Dias: "personalizado", DiasMin: "0", DiasMax: "0")).IsValid);
        Assert.True(_validator.Validate(new ListarSolicitudesQuery(Dias: "CUSTOM", DiasMin: "5", DiasMax: "30")).IsValid);

        var sinLimites = _validator.Validate(new ListarSolicitudesQuery(Dias: "custom"));
        Assert.False(sinLimites.IsValid);
        Assert.Contains(sinLimites.Errors, e => e.PropertyName == "dias");

        foreach (var badNum in new[] { "", "   ", "\u0000", "-1", "+5", "1000", "abc" })
        {
            Assert.Contains(
                _validator.Validate(new ListarSolicitudesQuery(Dias: "custom", DiasMin: badNum)).Errors,
                e => e.PropertyName == "dias_min");
            Assert.Contains(
                _validator.Validate(new ListarSolicitudesQuery(Dias: "custom", DiasMax: badNum)).Errors,
                e => e.PropertyName == "dias_max");
        }

        var invertido = _validator.Validate(new ListarSolicitudesQuery(Dias: "custom", DiasMin: "15", DiasMax: "10"));
        Assert.False(invertido.IsValid);
        Assert.Contains(invertido.Errors, e => e.PropertyName == "dias_max");
    }

    [Fact]
    public void Validate_SortDirPageYSizeInvalidos_FallanConPropiedadesExactas()
    {
        foreach (var s in new[] { "vencimiento", "folio", "entrega", "dias", "FOLIO" })
        {
            Assert.True(_validator.Validate(new ListarSolicitudesQuery(Sort: s)).IsValid);
        }

        foreach (var badSort in new[] { "", "\u0000", "asunto", "id" })
        {
            Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(Sort: badSort)).Errors,
                e => e.PropertyName == "sort");
        }

        foreach (var badDir in new[] { "", "\u0000", "up", "ascendente" })
        {
            Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(Dir: badDir)).Errors,
                e => e.PropertyName == "dir");
        }

        foreach (var badPage in new[] { "", "\u0000", "0", "-1", "+1", "abc", "999999999999999" })
        {
            Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(Page: badPage)).Errors,
                e => e.PropertyName == "page");
        }

        foreach (var validSize in new[] { "10", "25", "50" })
        {
            Assert.True(_validator.Validate(new ListarSolicitudesQuery(Size: validSize)).IsValid);
        }

        foreach (var badSize in new[] { "", "\u0000", "0", "5", "15", "20", "100" })
        {
            Assert.Contains(_validator.Validate(new ListarSolicitudesQuery(Size: badSize)).Errors,
                e => e.PropertyName == "size");
        }
    }
}
