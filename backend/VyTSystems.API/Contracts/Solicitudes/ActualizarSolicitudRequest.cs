using System.Text.Json.Serialization;

namespace VyTSystems.API.Contracts.Solicitudes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActualizarSolicitudRequest
{
    public string? FechaEntrega { get; init; }
    public string? HoraEntrega { get; init; }
    public string? AreaReceptoraId { get; init; }
    public string? Asunto { get; init; }
    public string? MedioEntrega { get; init; }
    public string? EstatusEntrega { get; init; }
    public string? TipoSolicitud { get; init; }
    public string? TipoEvidencia { get; init; }
    public bool? TieneAcuse { get; init; }
    public string? TipoAcuse { get; init; }
    public string? FechaVencimiento { get; init; }
}
