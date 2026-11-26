using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Abstractions.Persistence;

/// <summary>
/// Datos normalizados requeridos por la capa de persistencia para registrar una solicitud de forma idempotente y transaccional.
/// </summary>
public sealed record RegistroSolicitudPersistRequest(
    string CreadoPorUsuarioId,
    string IdempotencyKey,
    string IdempotencyPayloadHash,
    bool GenerarFolioAutomatico,
    string? FolioManual,
    Guid AreaReceptoraId,
    int AnioFolioAutomatico);

/// <summary>
/// Resultado operativo de la transacción de registro de una solicitud.
/// </summary>
public enum RegistroSolicitudEstado
{
    Creada = 1,
    RecuperadaPorIdempotencia = 2,
    IdempotenciaConflicto = 3,
    FolioDuplicado = 4,
    AreaReceptoraInvalida = 5,
    PersistenciaNoDisponible = 6
}

/// <summary>
/// Desenlace tipado de la operación atómica de creación e idempotencia de solicitudes.
/// </summary>
public sealed record RegistroSolicitudOutcome(
    RegistroSolicitudEstado Estado,
    Solicitud? Solicitud = null)
{
    public static RegistroSolicitudOutcome Creada(Solicitud solicitud) =>
        new(RegistroSolicitudEstado.Creada, solicitud);

    public static RegistroSolicitudOutcome RecuperadaPorIdempotencia(Solicitud solicitud) =>
        new(RegistroSolicitudEstado.RecuperadaPorIdempotencia, solicitud);

    public static RegistroSolicitudOutcome IdempotenciaConflicto() =>
        new(RegistroSolicitudEstado.IdempotenciaConflicto);

    public static RegistroSolicitudOutcome FolioDuplicado() =>
        new(RegistroSolicitudEstado.FolioDuplicado);

    public static RegistroSolicitudOutcome AreaReceptoraInvalida() =>
        new(RegistroSolicitudEstado.AreaReceptoraInvalida);

    public static RegistroSolicitudOutcome PersistenciaNoDisponible() =>
        new(RegistroSolicitudEstado.PersistenciaNoDisponible);
}

/// <summary>
/// Contrato de persistencia atómica para registrar nuevas solicitudes con control de idempotencia y concurrencia.
/// </summary>
public interface ISolicitudRegistroPersistence
{
    Task<RegistroSolicitudOutcome> EjecutarCreacionAsync(
        RegistroSolicitudPersistRequest request,
        Func<string, Solicitud> construirSolicitud,
        CancellationToken cancellationToken = default);
}
