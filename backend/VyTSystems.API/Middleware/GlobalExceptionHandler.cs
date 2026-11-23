using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VyTSystems.Domain.Common;

namespace VyTSystems.API.Middleware;

/// <summary>
/// Manejador global de excepciones bajo formato estándar ProblemDetails (RFC 7807).
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DomainException domainException)
        {
            var domainProblem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regla de negocio incumplida",
                Detail = domainException.Message,
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(domainProblem, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Excepción no controlada en {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno del servidor",
            Detail = environment.IsDevelopment()
                ? exception.Message
                : "Ocurrió un error inesperado al procesar la solicitud.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}
