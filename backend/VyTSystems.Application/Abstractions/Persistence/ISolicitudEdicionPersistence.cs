using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Parámetros requeridos para ejecutar la actualización atómica con control de concurrencia optimista.
/// </summary>
public sealed record EdicionSolicitudPersistRequest(
    Guid SolicitudId,
    string VersionConcurrenciaEsperada,
    Guid AreaReceptoraId);

/// <summary>
/// Resultado operativo de la transacción de edición de una solicitud.
/// </summary>
public enum EdicionSolicitudEstado
{
    Actualizada = 1,
    NoEncontrada = 2,
    ConflictoConcurrencia = 3,
    AreaReceptoraInvalida = 4,
    PersistenciaNoDisponible = 5
}

/// <summary>
/// Desenlace tipado de la actualización atómica de una solicitud y rotación de su versión de concurrencia.
/// </summary>
public sealed record EdicionSolicitudOutcome(
    EdicionSolicitudEstado Estado,
    Solicitud? Solicitud = null,
    string? VersionConcurrenciaActual = null)
{
    public static EdicionSolicitudOutcome Actualizada(Solicitud solicitud, string versionConcurrenciaActual) =>
        new(EdicionSolicitudEstado.Actualizada, solicitud, versionConcurrenciaActual);

    public static EdicionSolicitudOutcome NoEncontrada() =>
        new(EdicionSolicitudEstado.NoEncontrada);

    public static EdicionSolicitudOutcome ConflictoConcurrencia() =>
        new(EdicionSolicitudEstado.ConflictoConcurrencia);

    public static EdicionSolicitudOutcome AreaReceptoraInvalida() =>
        new(EdicionSolicitudEstado.AreaReceptoraInvalida);

    public static EdicionSolicitudOutcome PersistenciaNoDisponible() =>
        new(EdicionSolicitudEstado.PersistenciaNoDisponible);
}

/// <summary>
/// Contrato de persistencia atómica para editar solicitudes existentes con control de concurrencia optimista.
/// </summary>
public interface ISolicitudEdicionPersistence
{
    Task<EdicionSolicitudOutcome> EjecutarEdicionAsync(
        EdicionSolicitudPersistRequest request,
        Action<Solicitud> actualizarSolicitud,
        CancellationToken cancellationToken = default);
}
