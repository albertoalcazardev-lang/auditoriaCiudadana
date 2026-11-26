using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Domain.Enums;

namespace VyTSystems.UnitTests.Application;

public sealed class SolicitudIdempotencyHasherTests
{
    private static readonly Guid AreaId = Guid.Parse("01950000-0000-7000-8000-000000000001");

    [Fact]
    public void CalcularHash_EsDeterministicoHexLower64YNormalizaFolioYAsunto()
    {
        var h1 = SolicitudIdempotencyHasher.CalcularHash(
            false,
            "  001-abc/2026 ",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15, 30),
            AreaId,
            "  Asunto de prueba  ",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 20));

        var h2 = SolicitudIdempotencyHasher.CalcularHash(
            false,
            "001-ABC/2026",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Asunto de prueba",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 20));

        Assert.Equal(64, h1.Length);
        Assert.Equal(h1, h1.ToLowerInvariant());
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void CalcularHash_CambiaCuandoCambiaCualquierCampoOModalidad()
    {
        var baseHash = SolicitudIdempotencyHasher.CalcularHash(
            false,
            "SOL-001",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Asunto",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 20));

        var autoHash = SolicitudIdempotencyHasher.CalcularHash(
            true,
            null,
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Asunto",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 20));

        var distintoAsunto = SolicitudIdempotencyHasher.CalcularHash(
            false,
            "SOL-001",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Otro asunto",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            true,
            TipoAcuse.Sello,
            new DateOnly(2026, 5, 20));

        Assert.NotEqual(baseHash, autoHash);
        Assert.NotEqual(baseHash, distintoAsunto);
    }

    [Fact]
    public void CalcularHash_EnModoAutomatico_IgnoraFolioParaNullNormalizado()
    {
        var h1 = SolicitudIdempotencyHasher.CalcularHash(
            true,
            null,
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Asunto",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            false,
            TipoAcuse.NoAplica,
            new DateOnly(2026, 5, 20));

        var h2 = SolicitudIdempotencyHasher.CalcularHash(
            true,
            "cualquier-valor",
            new DateOnly(2026, 5, 10),
            new TimeOnly(9, 15),
            AreaId,
            "Asunto",
            MedioEntrega.Digital,
            EstatusEntrega.EnProceso,
            TipoSolicitud.Informacion,
            TipoEvidencia.Carpeta,
            false,
            TipoAcuse.NoAplica,
            new DateOnly(2026, 5, 20));

        Assert.Equal(h1, h2);
    }
}
