using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using VyTSystems.Application.Solicitudes.Catalogos;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Application.Solicitudes.Crear;
using VyTSystems.Application.Solicitudes.Editar;
using VyTSystems.Application.Solicitudes.Eliminar;
using VyTSystems.Application.Solicitudes.Listar;
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
        new(NormalizeSingleQueryParam(folio, queryValueCount));

    public static ListarSolicitudesQuery ToListarQuery(
        IQueryCollection queryCollection,
        string? q = null,
        string? entD = null,
        string? entH = null,
        string? venD = null,
        string? venH = null,
        string? dias = null,
        string? diasMin = null,
        string? diasMax = null,
        string? sort = null,
        string? dir = null,
        string? page = null,
        string? size = null) =>
        new(
            ReadSingleQueryParam(queryCollection, "q", q),
            ReadSingleQueryParam(queryCollection, "ent_d", entD),
            ReadSingleQueryParam(queryCollection, "ent_h", entH),
            ReadSingleQueryParam(queryCollection, "ven_d", venD),
            ReadSingleQueryParam(queryCollection, "ven_h", venH),
            ReadSingleQueryParam(queryCollection, "dias", dias),
            ReadSingleQueryParam(queryCollection, "dias_min", diasMin),
            ReadSingleQueryParam(queryCollection, "dias_max", diasMax),
            ReadSingleQueryParam(queryCollection, "sort", sort),
            ReadSingleQueryParam(queryCollection, "dir", dir),
            ReadSingleQueryParam(queryCollection, "page", page),
            ReadSingleQueryParam(queryCollection, "size", size));

    public static EliminarSolicitudCommand ToEliminarCommand(
        string usuarioId,
        string? solicitudId,
        StringValues ifMatchHeaders) =>
        new(
            usuarioId,
            solicitudId,
            ParseIfMatchToVersion(ifMatchHeaders));

    public static string? NormalizeSingleQueryParam(string? value, int queryValueCount) =>
        queryValueCount switch
        {
            0 => null,
            1 => value,
            _ => InvalidSentinel
        };

    private static string? ReadSingleQueryParam(
        IQueryCollection queryCollection,
        string key,
        string? boundValue)
    {
        var rawValues = queryCollection[key];
        var singleRaw = rawValues.Count == 1 ? (rawValues[0] ?? boundValue ?? string.Empty) : boundValue;
        return NormalizeSingleQueryParam(singleRaw, rawValues.Count);
    }

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

    public static ListadoSolicitudesResponse ToResponse(this ListarSolicitudesResult result) =>
        new(
            result.Items
                .Select(dto => new SolicitudListadoItemResponse(
                    dto.Id.ToString(),
                    dto.Folio,
                    dto.Asunto,
                    dto.AreaReceptora,
                    dto.MedioEntrega,
                    dto.EstatusEfectivo,
                    dto.FechaEntrega,
                    dto.FechaVencimiento,
                    dto.DiasRestantes,
                    FormatStrongETag(dto.VersionConcurrencia)))
                .ToArray(),
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages);

    private static IReadOnlyList<CatalogoOpcionResponse> MapearOpciones(IReadOnlyList<CatalogoOpcionDto> opciones) =>
        opciones.Select(o => new CatalogoOpcionResponse(o.Codigo, o.Nombre)).ToArray();
}
