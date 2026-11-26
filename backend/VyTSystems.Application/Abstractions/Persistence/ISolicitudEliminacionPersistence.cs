namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Parámetros requeridos para ejecutar la eliminación física controlada de una solicitud bajo concurrencia optimista.
/// </summary>
public sealed record EliminacionSolicitudPersistRequest(
    Guid SolicitudId,
    string VersionConcurrenciaEsperada,
    DateTime EliminadoEnUtc);

/// <summary>
/// Resultado operativo de la transacción de eliminación controlada de una solicitud.
/// </summary>
public enum EliminacionSolicitudEstado
{
    Eliminada = 1,
    NoEncontrada = 2,
    PrecondicionFallida = 3,
    ConflictoIntegridad = 4,
    PersistenciaNoDisponible = 5
}

/// <summary>
/// Desenlace tipado de la operación atómica de eliminación de solicitud y registro de tombstone de idempotencia.
/// </summary>
public sealed record EliminacionSolicitudOutcome(EliminacionSolicitudEstado Estado)
{
    public static EliminacionSolicitudOutcome Eliminada() =>
        new(EliminacionSolicitudEstado.Eliminada);

    public static EliminacionSolicitudOutcome NoEncontrada() =>
        new(EliminacionSolicitudEstado.NoEncontrada);

    public static EliminacionSolicitudOutcome PrecondicionFallida() =>
        new(EliminacionSolicitudEstado.PrecondicionFallida);

    public static EliminacionSolicitudOutcome ConflictoIntegridad() =>
        new(EliminacionSolicitudEstado.ConflictoIntegridad);

    public static EliminacionSolicitudOutcome PersistenciaNoDisponible() =>
        new(EliminacionSolicitudEstado.PersistenciaNoDisponible);
}

/// <summary>
/// Contrato de persistencia atómica para eliminar físicamente solicitudes preservando concurrencia optimista e idempotencia histórica.
/// </summary>
public interface ISolicitudEliminacionPersistence
{
    Task<EliminacionSolicitudOutcome> EjecutarEliminacionAsync(
        EliminacionSolicitudPersistRequest request,
        CancellationToken cancellationToken = default);
}
