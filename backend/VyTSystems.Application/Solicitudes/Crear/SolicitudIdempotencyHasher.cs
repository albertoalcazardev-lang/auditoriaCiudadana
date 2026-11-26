using System.Security.Cryptography;
using System.Text.Json;
using VyTSystems.Domain.Entities;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Genera un hash SHA-256 determinístico sobre la representación canónica normalizada del comando de creación.
/// </summary>
public static class SolicitudIdempotencyHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private sealed record CanonicalSolicitudPayload(
        bool GenerarFolioAutomatico,
        string? FolioNormalizado,
        string FechaEntrega,
        string HoraEntrega,
        Guid AreaReceptoraId,
        string Asunto,
        string MedioEntrega,
        string EstatusEntrega,
        string TipoSolicitud,
        string TipoEvidencia,
        bool TieneAcuse,
        string TipoAcuse,
        string FechaVencimiento);

    public static string CalcularHash(
        bool generarFolioAutomatico,
        string? folio,
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        Guid areaReceptoraId,
        string asunto,
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        bool tieneAcuse,
        TipoAcuse tipoAcuse,
        DateOnly fechaVencimiento)
    {
        var folioNormalizado = generarFolioAutomatico
            ? null
            : Solicitud.NormalizarFolio(folio!);

        var horaNormalizada = new TimeOnly(horaEntrega.Hour, horaEntrega.Minute);

        var canonical = new CanonicalSolicitudPayload(
            generarFolioAutomatico,
            folioNormalizado,
            fechaEntrega.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            horaNormalizada.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            areaReceptoraId,
            asunto.Trim(),
            medioEntrega.ToString(),
            estatusEntrega.ToString(),
            tipoSolicitud.ToString(),
            tipoEvidencia.ToString(),
            tieneAcuse,
            tipoAcuse.ToString(),
            fechaVencimiento.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(canonical, SerializerOptions);
        var hashBytes = SHA256.HashData(jsonBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
