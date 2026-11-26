using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.Eliminar;

/// <summary>
/// Comando CQRS para eliminar físicamente una solicitud existente bajo concurrencia optimista obligatoria.
/// </summary>
public sealed record EliminarSolicitudCommand(
    string UsuarioId,
    string? SolicitudId,
    string? VersionConcurrencia) : IRequest<Result>;
