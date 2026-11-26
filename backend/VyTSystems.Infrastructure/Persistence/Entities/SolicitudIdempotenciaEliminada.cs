namespace VyTSystems.Infrastructure.Persistence.Entities;

/// <summary>
/// Registro tombstone para preservar el consumo histórico de claves de idempotencia de solicitudes eliminadas.
/// </summary>
internal sealed class SolicitudIdempotenciaEliminada
{
    public string CreadoPorUsuarioId { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string IdempotencyPayloadHash { get; set; } = null!;
    public Guid SolicitudId { get; set; }
    public DateTime EliminadoEnUtc { get; set; }
}
