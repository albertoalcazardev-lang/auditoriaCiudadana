using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.Editar;

/// <summary>
/// Comando CQRS para editar los datos de una solicitud existente bajo control de concurrencia optimista.
/// </summary>
public sealed record EditarSolicitudCommand(
    string UsuarioId,
    string? SolicitudId,
    string? VersionConcurrencia,
    string? FechaEntrega,
    string? HoraEntrega,
    string? AreaReceptoraId,
    string? Asunto,
    string? MedioEntrega,
    string? EstatusEntrega,
    string? TipoSolicitud,
    string? TipoEvidencia,
    bool? TieneAcuse,
    string? TipoAcuse,
    string? FechaVencimiento) : IRequest<Result<EditarSolicitudResult>>;
