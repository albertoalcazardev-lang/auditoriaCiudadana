using MapsterMapper;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.Common;

/// <summary>
/// Utilidades compartidas para resolver la fecha local en Ciudad de México y proyectar solicitudes en modo edición.
/// </summary>
internal static class SolicitudZonaHoraria
{
    private static readonly TimeZoneInfo MexicoTimeZone = ResolverZonaHorariaMexico();

    public static TimeZoneInfo ResolverZonaHorariaMexico()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("America/Mexico_City", out var ianaZone))
        {
            return ianaZone;
        }

        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
    }

    public static (DateTime AhoraUtc, DateOnly HoyMexico) ObtenerAhoraYHoyMexico(TimeProvider timeProvider)
    {
        var ahoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var ahoraCdmx = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, MexicoTimeZone);
        return (ahoraUtc, DateOnly.FromDateTime(ahoraCdmx));
    }

    public static SolicitudEdicionDto MapearSolicitudEdicion(
        IMapper mapper,
        Solicitud solicitud,
        DateOnly hoyMexico)
    {
        var dtoBase = mapper.Map<SolicitudEdicionDto>(solicitud);
        return dtoBase with
        {
            DiasRestantes = solicitud.CalcularDiasRestantes(hoyMexico),
            EstatusEfectivo = solicitud.ObtenerEstatusEfectivo(hoyMexico).ToString()
        };
    }
}
