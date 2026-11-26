namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Proyección de salida de una solicitud registrada con su estatus efectivo y días restantes calculados.
/// </summary>
public sealed record SolicitudCreadaDto(
    Guid Id,
    string Folio,
    DateOnly FechaVencimiento,
    int DiasRestantes,
    string Estatus);

/// <summary>
/// Resultado del caso de uso de creación indicando si la respuesta proviene de un reintento idempotente.
/// </summary>
public sealed record CrearSolicitudResult(
    SolicitudCreadaDto Solicitud,
    bool FueRecuperadaPorIdempotencia);
