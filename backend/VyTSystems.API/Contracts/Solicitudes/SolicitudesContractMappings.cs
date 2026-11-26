using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Application.Solicitudes.Editar;
using VyTSystems.Application.Solicitudes.ObtenerParaEdicion;

namespace VyTSystems.API.Contracts.Solicitudes;

internal static partial class SolicitudesContractMappings
{
    private const string InvalidSentinel = "\u0000";

    [GeneratedRegex(@"^""([\x21\x23-\x7E]{1,128})""$", RegexOptions.CultureInvariant)]
    private static partial Regex StrongETagRegex();

    public static CrearSolicitudCommand ToCommand(
        this CrearSolicitudRequest request,
        string usuarioId,
        string? idempotencyKey) =>
        new(
            usuarioId,
            idempotencyKey,
            request.GenerarFolioAutomatico,
            request.Folio,
            request.FechaEntrega,
            request.HoraEntrega,
            request.AreaReceptoraId,
            request.Asunto,
            request.MedioEntrega,
            request.EstatusEntrega,
            request.TipoSolicitud,
            request.TipoEvidencia,
            request.TieneAcuse,
            request.TipoAcuse,
            request.FechaVencimiento);

    public static ObtenerSolicitudParaEdicionQuery ToEdicionQuery(string? folio, int queryValueCount) =>
        queryValueCount switch
        {
            0 => new ObtenerSolicitudParaEdicionQuery(null),
            1 => new ObtenerSolicitudParaEdicionQuery(folio),
            _ => new ObtenerSolicitudParaEdicionQuery(InvalidSentinel)
        };

    public static EditarSolicitudCommand ToCommand(
        this ActualizarSolicitudRequest request,
        string usuarioId,
        string? solicitudId,
        StringValues ifMatchHeaders) =>
        new(
            usuarioId,
            solicitudId,
            ParseIfMatchToVersion(ifMatchHeaders),
            request.FechaEntrega,
            request.HoraEntrega,
            request.AreaReceptoraId,
            request.Asunto,
            request.MedioEntrega,
            request.EstatusEntrega,
            request.TipoSolicitud,
            request.TipoEvidencia,
            request.TieneAcuse,
            request.TipoAcuse,
            request.FechaVencimiento);

    public static string? ParseIfMatchToVersion(StringValues ifMatchHeaders)
    {
        if (ifMatchHeaders.Count == 0)
        {
            return null;
        }

        if (ifMatchHeaders.Count != 1)
        {
            return InvalidSentinel;
        }

        var raw = ifMatchHeaders[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Any(c => char.IsControl(c) && c != '\t'))
        {
            return InvalidSentinel;
        }

        var match = StrongETagRegex().Match(raw.Trim(' ', '\t'));
        if (!match.Success)
        {
            return InvalidSentinel;
        }

        var version = match.Groups[1].Value;
        return string.Equals(version, "*", StringComparison.Ordinal)
            ? InvalidSentinel
            : version;
    }

    public static string FormatStrongETag(string versionConcurrencia) =>
        $"\"{versionConcurrencia}\"";

    public static SolicitudesCatalogosResponse ToResponse(this SolicitudesCatalogosDto dto) =>
        new(
            dto.AreasReceptoras
                .Select(a => new AreaReceptoraResponse(a.Id.ToString(), a.Nombre))
                .ToArray(),
            MapearOpciones(dto.MediosEntrega),
            MapearOpciones(dto.EstatusEntrega),
            MapearOpciones(dto.TiposSolicitud),
            MapearOpciones(dto.TiposEvidencia),
            MapearOpciones(dto.TiposAcuse));

    public static CrearSolicitudResponse ToResponse(this SolicitudCreadaDto dto) =>
        new(
            dto.Id.ToString(),
            dto.Folio,
            dto.FechaVencimiento,
            dto.DiasRestantes,
            dto.Estatus);

    public static SolicitudEdicionResponse ToEdicionResponse(this SolicitudEdicionDto dto) =>
        new(
            dto.Id.ToString(),
            dto.Folio,
            dto.FechaEntrega,
            dto.HoraEntrega.ToString("HH:mm", CultureInfo.InvariantCulture),
            dto.AreaReceptoraId.ToString(),
            dto.Asunto,
            dto.MedioEntrega,
            dto.EstatusEntrega,
            dto.TipoSolicitud,
            dto.TipoEvidencia,
            dto.TieneAcuse,
            dto.TipoAcuse,
            dto.FechaVencimiento,
            dto.DiasRestantes,
            dto.EstatusEfectivo);

    private static IReadOnlyList<CatalogoOpcionResponse> MapearOpciones(IReadOnlyList<CatalogoOpcionDto> opciones) =>
        opciones.Select(o => new CatalogoOpcionResponse(o.Codigo, o.Nombre)).ToArray();
}
